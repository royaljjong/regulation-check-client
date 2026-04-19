// =============================================================================
// Program.cs
// ASP.NET Core Web API 시작점 및 DI 컨테이너 구성
//
// [등록 요약]
//   FluentValidation    → CoordinateRequestValidator, ParcelSearchRequestValidator
//   HealthChecks        → SpatialDataHealthCheck (GET /health)
//   IZoningLayerProvider            → ShapefileZoningLayerProvider         (Singleton)
//   ILawReferenceProvider           → OpenLawLawReferenceProvider           (Transient)
//   IDevelopmentRestrictionProvider → ShapefileDevRestrictionProvider       (Singleton, UQ141 그린벨트)
//   IDevActRestrictionProvider      → ShapefileDevActRestrictionProvider    (Singleton, UQ171 개발행위허가제한지역)
//   IDistrictUnitPlanProvider       → NullDistrictUnitPlanProvider          (Transient, 지구단위계획 미연동)
//   IParcelSearchProvider           → StubParcelSearchProvider              (Transient)
//   IAddressResolver                → StubAddressResolver                   (Transient)
//   IRegulationCheckService         → RegulationCheckService                (Scoped)
//   ShapefileLoader / ZoningFeatureCache / CoordinateContainmentChecker → Singleton
// =============================================================================

using System.Reflection;
using System.Text.Json.Serialization;
using AutomationRawCheck.Api.Middleware;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Application.Services;
using AutomationRawCheck.Application.Validation;
using AutomationRawCheck.Infrastructure.Address;
using AutomationRawCheck.Infrastructure.Configuration;
using AutomationRawCheck.Infrastructure.ExtraLayers;
using AutomationRawCheck.Infrastructure.Health;
using AutomationRawCheck.Infrastructure.Law;
using AutomationRawCheck.Infrastructure.Parcel;
using AutomationRawCheck.Infrastructure.Spatial;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using NetTopologySuite.Geometries;
using Serilog;
using System.Text.Json;

#region Serilog 부트스트랩 로거

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(
        new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
                optional: true)
            .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

#endregion

try
{
    Log.Information("========================================");
    Log.Information("건축/토지 법규 검토 백엔드 서버 시작...");
    Log.Information("========================================");

    // Railway / Render 등 클라우드 환경에서 PORT 환경변수 자동 처리
    var port = Environment.GetEnvironmentVariable("PORT");
    var builder = WebApplication.CreateBuilder(args);
    if (!string.IsNullOrEmpty(port))
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    #region Serilog 호스트 등록

    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext());

    #endregion

    #region CORS 설정 (Vercel 프론트엔드 허용)

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendPolicy", policy =>
        {
            // VITE_FRONTEND_ORIGIN 환경변수로 허용 도메인 지정 (예: https://your-app.vercel.app)
            // 미설정 시 모든 출처 허용 (개발/데모 용도)
            var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
                ?? Array.Empty<string>();

            if (allowedOrigins.Length > 0)
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            else
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
        });
    });

    #endregion

    #region 설정 바인딩

    builder.Services.Configure<SpatialDataOptions>(
        builder.Configuration.GetSection(SpatialDataOptions.SectionName));

    builder.Services.Configure<LawApiOptions>(
        builder.Configuration.GetSection(LawApiOptions.SectionName));

    #endregion

    #region 캐시 / NTS 공통 의존성

    builder.Services.AddMemoryCache();

    // NTS GeometryFactory: WGS84 (EPSG:4326) — API 입력 좌표 파싱용
    // SHP 데이터는 EPSG:5174(Korean TM)이며, CoordinateContainmentChecker에서
    // ProjNet4GeoAPI를 사용해 WGS84 → EPSG:5174 변환 후 Point-in-Polygon 판정합니다.
    builder.Services.AddSingleton(_ => new GeometryFactory(new PrecisionModel(), 4326));

    #endregion

    #region HttpClient 등록

    // 법제처 API용 HttpClient
    builder.Services.AddHttpClient("LawApi", client =>
    {
        var lawOpts = builder.Configuration
            .GetSection(LawApiOptions.SectionName)
            .Get<LawApiOptions>() ?? new LawApiOptions();

        client.BaseAddress = new Uri(lawOpts.BaseUrl);
        client.Timeout     = TimeSpan.FromSeconds(lawOpts.TimeoutSeconds);
    });

    // V-World Data API용 HttpClient (지구단위계획구역 overlay)
    // Referer 헤더: V-World API는 등록된 도메인의 Referer 헤더로 인증키를 검증합니다.
    builder.Services.AddHttpClient("VWorldData", client =>
    {
        var vwOpts = builder.Configuration
            .GetSection(VWorldOptions.SectionName)
            .Get<VWorldOptions>() ?? new VWorldOptions();

        client.Timeout = TimeSpan.FromSeconds(vwOpts.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Referer", "http://localhost");
    });

    // VWorldOptions DI 등록
    builder.Services.Configure<VWorldOptions>(
        builder.Configuration.GetSection(VWorldOptions.SectionName));

    // VWorldApi (개발제한구역 API) DI 등록
    builder.Services.Configure<VWorldApiOptions>(
        builder.Configuration.GetSection(VWorldApiOptions.SectionName));

    builder.Services.Configure<DevelopmentActionPermitApiOptions>(
        builder.Configuration.GetSection(DevelopmentActionPermitApiOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<DevelopmentActionPermitApiOptions>, DevelopmentActionPermitApiOptionsValidator>();
    builder.Services.Configure<ReviewSnapshotStoreOptions>(
        builder.Configuration.GetSection(ReviewSnapshotStoreOptions.SectionName));
    builder.Services.Configure<AiAssistOptions>(
        builder.Configuration.GetSection(AiAssistOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<AiAssistOptions>, AiAssistOptionsValidator>();
    builder.Services.Configure<RegulationResearchOptions>(
        builder.Configuration.GetSection(RegulationResearchOptions.SectionName));
    builder.Services.AddSingleton<IValidateOptions<RegulationResearchOptions>, RegulationResearchOptionsValidator>();

    // VWorld 개발제한구역 전용 HttpClient (타임아웃 5초)
    builder.Services.AddHttpClient("vworld-dev-restriction", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    builder.Services.AddHttpClient("development-action-permit-api", client =>
    {
        var apiOptions = builder.Configuration
            .GetSection(DevelopmentActionPermitApiOptions.SectionName)
            .Get<DevelopmentActionPermitApiOptions>() ?? new DevelopmentActionPermitApiOptions();

        client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    builder.Services.AddHttpClient("ai-assist-runner", client =>
    {
        var options = builder.Configuration
            .GetSection(AiAssistOptions.SectionName)
            .Get<AiAssistOptions>() ?? new AiAssistOptions();

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
            client.BaseAddress = new Uri(options.Endpoint);

        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    builder.Services.AddHttpClient("regulation-research-live", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    #endregion

    #region Infrastructure — 공간 데이터 헬퍼 (Singleton)

    builder.Services.AddSingleton<ShapefileLoader>();
    builder.Services.AddSingleton<ZoningFeatureCache>();
    builder.Services.AddSingleton<CoordinateContainmentChecker>();
    builder.Services.AddSingleton<ICityPlanFacilityGeometryService, CityPlanFacilityGeometryService>();

    #endregion

    #region Infrastructure — 프로바이더 등록

    // ── 용도지역 공간 레이어 (Singleton) ─────────────────────────────────────
    // TODO: 외부 공간 API 연동 시 ShapefileZoningLayerProvider → 새 구현체로 교체
    builder.Services.AddSingleton<IZoningLayerProvider, ShapefileZoningLayerProvider>();

    // ── 법령 참조 (Transient) ─────────────────────────────────────────────────
    // OpenLawLawReferenceProvider: 법제처 DRF API 실구현체 (3단계: 법령검색 → MST → 조문)
    // 실패 시 빈 목록 반환으로 fallback — 전체 API 정상 동작 보장.
    // 비활성화: 아래 줄을 StubLawReferenceProvider로 교체하세요.
    builder.Services.AddTransient<ILawReferenceProvider, OpenLawLawReferenceProvider>();

    // ── normalizedKey 기반 조문 조회 (Scoped) ─────────────────────────────────
    // OpenLawClauseProvider: legalBasis.normalizedKey → 법제처 DRF API 조문 텍스트 조회
    // 캐싱: IMemoryCache (MST 72h, 조문 24h, 실패 1h)
    // 사용: POST /api/regulation-check/law-layers?includeLegalBasis=true
    //        POST /api/regulation-check/review-items?includeLegalBasis=true
    // 활성화: appsettings.json "LawApi:ClauseEnabled" = true + "LawApi:OcValue" 설정
    builder.Services.AddScoped<ILawClauseProvider, OpenLawClauseProvider>();

    // ── 개발행위허가제한지역 (Singleton) ──────────────────────────────────────
    // ShapefileDevActRestrictionProvider: UQ171 레이어(UQQ900) 실구현체 (지연 로딩 Singleton)
    // ※ UQ171 파일명은 UPIS 표준상 지구단위계획 코드이나, 이 데이터셋에는 개발행위허가제한지역 데이터
    builder.Services.AddSingleton<IDevActRestrictionProvider, ShapefileDevActRestrictionProvider>();

    // ── 개발제한구역 (Singleton) ──────────────────────────────────────────────
    // ShapefileDevRestrictionProvider: UQ141 레이어(UDV100) 실구현체 (지연 로딩 Singleton)
    builder.Services.AddSingleton<IDevelopmentRestrictionProvider, ShapefileDevRestrictionProvider>();

    // ── VWorld 개발제한구역 API 보조 프로바이더 (Scoped) ──────────────────────
    // VWorldApiOptions.Enabled == false 이면 호출되지 않으므로 항상 등록합니다.
    builder.Services.AddScoped<VWorldDevRestrictionProvider>();

    // ── 지구단위계획 (Transient, V-World WFS 실연동) ──────────────────────────
    // WfsDistrictUnitPlanProvider: V-World WFS CQL_FILTER(INTERSECTS) 기반 Point-in-Polygon 판정
    // API 키 미설정 또는 오류 시 자동으로 DataUnavailable 반환 (메인 API 영향 없음)
    // API 키: appsettings.json VWorld:ApiKey 또는 환경변수 VWorld__ApiKey
    builder.Services.AddTransient<IDistrictUnitPlanProvider, WfsDistrictUnitPlanProvider>();

    // ── 지번/주소 → 좌표 변환 고수준 프로바이더 (Transient) ──────────────────
    // TODO: 외부 주소 API 연동 시 교체
    builder.Services.AddTransient<IParcelSearchProvider, VWorldParcelSearchProvider>();
    builder.Services.AddTransient<IParcelBoundaryGeometryService, VWorldParcelBoundaryGeometryService>();

    // ── 주소 해석기 — IAddressResolver (Transient) ───────────────────────────
    // VWorldAddressResolver: V-World 주소 좌표 검색 API 실구현체
    //   도로명 우선 → 미발견 시 지번 재시도 → 실패 시 null 반환
    //   API 키: appsettings.json VWorld:ApiKey (WFS와 동일 키 공유)
    builder.Services.AddTransient<IAddressResolver, VWorldAddressResolver>();

    #endregion

    #region Application 서비스

    builder.Services.AddScoped<IRegulationCheckService, RegulationCheckService>();
    builder.Services.AddSingleton<IRegulationResearchService, RegulationResearchService>();
    builder.Services.AddScoped<IReviewReportRenderer, ReviewReportRenderer>();
    builder.Services.AddScoped<DevelopmentActionPermitApiProvider>();
    builder.Services.AddScoped<CsvInputAutomationService>();
    builder.Services.AddSingleton<ICsvInputAutomationStore, InMemoryCsvInputAutomationStore>();
    builder.Services.AddSingleton<IAiAssistService, Gemma4AiAssistService>();
    builder.Services.AddSingleton<IReviewSnapshotStore, JsonFileReviewSnapshotStore>();

    #endregion

    #region 워밍업 서비스 (앱 시작 시 SHP 선로드)

    // 앱 기동 직후 백그라운드에서 SHP 전체 로드 트리거
    // → 헬스체크 cacheLoaded: true, 첫 API 요청 콜드 스타트 제거
    builder.Services.AddHostedService<SpatialDataWarmupService>();

    #endregion

    #region Health Checks

    builder.Services.AddHealthChecks()
        .AddCheck<SpatialDataHealthCheck>(
            name: "spatial-data",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "spatial", "data" });

    #endregion

    #region FluentValidation 등록

    // AddFluentValidationAutoValidation: 컨트롤러 액션 실행 전 자동 검사
    // AddFluentValidationClientsideAdapters: 클라이언트 사이드 어댑터 (선택)
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<CoordinateRequestValidator>();

    #endregion

    #region 컨트롤러 등록

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            // Enum을 문자열로 직렬화 (예: "Preliminary" 대신 숫자 0 방지)
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    #endregion

    #region Swagger 등록

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title   = "건축/토지 법규 검토 지원 API",
            Version = "v1",
            Description =
                "## 개요\n" +
                "로컬 공간데이터(SHP/CSV) 기반으로 좌표를 입력하면 " +
                "용도지역 정보와 관련 법령 근거를 통합 정리해 반환하는 백엔드 API입니다.\n\n" +
                "## 주의사항\n" +
                "- 본 결과는 **참고용 1차 판정**이며 실제 건축 허가 판단의 근거로 사용할 수 없습니다.\n" +
                "- 건폐율/용적률 확정값은 제공하지 않습니다.\n" +
                "- 지구단위계획, 개발제한구역, 자치법규, 개별 법령 추가 검토가 필요합니다.\n\n" +
                "## 엔드포인트\n" +
                "- `POST /api/regulation-check/coordinate` : **[MVP 핵심]** 좌표 기반 법규 1차 검토\n" +
                "- `POST /api/regulation-check/parcel` : 지번/주소 기반 검토 (주소 타입: placeholder)\n" +
                "- `GET  /api/regulation-check/health` : 컨트롤러 헬스체크\n" +
                "- `GET  /health` : 시스템 헬스체크 (공간 데이터 로드 상태 포함)",
            Contact = new OpenApiContact { Name = "법규 검토 백엔드 프로젝트" }
        });

        // Swagger Annotations 활성화 ([SwaggerSchema] 등)
        c.EnableAnnotations();

        // XML 주석 포함
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    });

    #endregion

    var app = builder.Build();

    #region 미들웨어 파이프라인

    // 전역 예외 처리 — 가장 먼저 등록
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // CORS — 컨트롤러보다 먼저 등록
    app.UseCors("FrontendPolicy");

    // Swagger UI
    // TODO: 외부 공개 전 프로덕션 환경에서는 인증 미들웨어 추가 또는 비활성화 고려
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "법규 검토 API v1");
        c.RoutePrefix   = "swagger";
        c.DocumentTitle = "법규 검토 API 문서";
        c.DefaultModelsExpandDepth(-1);
    });

    // 시스템 헬스체크 엔드포인트: GET /health
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json";

            var result = new
            {
                status  = report.Status.ToString(),
                checks  = report.Entries.Select(e => new
                {
                    name        = e.Key,
                    status      = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    data        = e.Value.Data
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            };

            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(result,
                    new JsonSerializerOptions { WriteIndented = true }));
        }
    });

    app.UseHttpsRedirection();
    app.MapControllers();

    #endregion

    Log.Information("서버 준비 완료. Swagger: /swagger | HealthCheck: /health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "서버 시작 실패: {Message}", ex.Message);
}
finally
{
    Log.CloseAndFlush();
}
