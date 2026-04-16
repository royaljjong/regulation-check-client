// =============================================================================
// SpatialDataWarmupService.cs
// 앱 시작 시 SHP 데이터를 미리 로드하는 백그라운드 서비스
//
// [역할]
//   IHostedService로 등록되어 앱 시작(ApplicationStarted) 직후 백그라운드에서
//   ShapefileZoningLayerProvider의 첫 로드를 트리거합니다.
//
// [효과]
//   - 헬스체크가 cacheLoaded: true / featureCount > 0 상태가 됨
//   - 실제 API 요청의 첫 응답이 빠름 (콜드 스타트 지연 제거)
//   - 로드 실패 시 서버는 계속 기동 (graceful degraded)
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region SpatialDataWarmupService 클래스

/// <summary>
/// 앱 시작 시 공간 데이터를 백그라운드로 미리 로드하는 호스티드 서비스입니다.
/// <para>
/// 실패 시 서버 기동을 중단하지 않으며, 다음 API 요청 시 재시도됩니다.
/// </para>
/// </summary>
public sealed class SpatialDataWarmupService : IHostedService
{
    #region 필드 및 생성자

    private readonly IZoningLayerProvider             _zoningProvider;
    private readonly ILogger<SpatialDataWarmupService> _logger;

    /// <summary>SpatialDataWarmupService를 초기화합니다.</summary>
    public SpatialDataWarmupService(
        IZoningLayerProvider              zoningProvider,
        ILogger<SpatialDataWarmupService> logger)
    {
        _zoningProvider = zoningProvider ?? throw new ArgumentNullException(nameof(zoningProvider));
        _logger         = logger         ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IHostedService 구현

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 백그라운드로 실행 — 서버 시작을 블록하지 않음
        _ = Task.Run(() => WarmUpAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion

    #region 워밍업 로직

    private async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("공간 데이터 워밍업 시작 (백그라운드)...");

            // 더미 좌표로 GetZoningAsync를 호출하면 내부에서 SHP 전체 로드가 트리거됨
            var dummyQuery = new CoordinateQuery(126.9784, 37.5665);
            await _zoningProvider.GetZoningAsync(dummyQuery, ct);

            _logger.LogInformation("공간 데이터 워밍업 완료.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("공간 데이터 워밍업 취소됨 (서버 종료).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "공간 데이터 워밍업 실패. 다음 API 요청 시 재시도됩니다.");
        }
    }

    #endregion
}

#endregion
