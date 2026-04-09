export default function ResultSummaryPanel({ 
  selectedCoordinate, lookupInfo, result, loading, error,
  savedResults = [], onSave, onRestore, onDeleteSaved 
}) {
  function formatCoord(v) { return typeof v === 'number' ? v.toFixed(6) : '-' }
  function toYesNo(v) { return v === true ? '예' : v === false ? '아니오' : '미확인' }

  // ── 신뢰도 판정 및 세부 설명 ─────────────────────────────────────
  function getConfidence(res) {
    if (!res?.zoning) {
      return { 
        level: 'LOW', 
        label: '신뢰도 낮음', 
        explanation: '데이터 미매칭 (참고용)',
        color: '#991b1b', bg: '#fee2e2' 
      };
    }
    const dist = res.debug?.nearestDistanceMeters || 0;
    if (dist > 0.1) {
      return { 
        level: 'MEDIUM', 
        label: '신뢰도 중간', 
        explanation: '경계 인접 또는 일부 불확실 요소',
        color: '#92400e', bg: '#fef3c7' 
      };
    }
    return { 
      level: 'HIGH', 
      label: '신뢰도 높음', 
      explanation: '데이터 직접 매칭 (법적 확정 아님)',
      color: '#166534', bg: '#f0fdf4' 
    };
  }

  // ── 매칭 사유를 사용자 친화적 문구로 변환 ──────────────────────
  function getDiagnosticMessage(reason, dist) {
    if (!reason) return "분석 데이터 매칭됨";
    if (dist > 0 && dist < 1.0) return "필지 경계선 인접 (정밀 확인)";
    if (reason.includes("Successful zoning hit")) return "공간 데이터와 직접 매칭됨";
    if (reason.includes("No matching feature")) return "범위 내 매칭 정보 없음";
    return "자동 분석 결과 (참고)";
  }

  // ── 핵심 상태 요약 생성 ─────────────────────────────────────────────
  function getCoreStatus() {
    if (loading || error || !result) return null;

    const hasRestriction = result.overlayFlags?.hasAnyRestriction;
    const isAddress = lookupInfo?.mode === 'address';
    const conf = getConfidence(result);

    return (
      <div style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 8 }}>
          <StatusBadge label={conf.label} bg={conf.bg} color={conf.color} border={`1px solid ${conf.color}20`} />
          <StatusBadge label={isAddress ? '주소 기준' : '좌표 기준'} bg="#f1f5f9" color="#475569" />
          <StatusBadge 
            label={hasRestriction ? '중첩 규제 있음' : '주요 규제 없음*'} 
            bg={hasRestriction ? '#fff1f2' : '#f0fdf4'} 
            color={hasRestriction ? '#be123c' : '#15803d'} 
          />
        </div>
        <div style={{ fontSize: 11, color: conf.color, fontWeight: 500, marginBottom: 4 }}>
          • {conf.explanation}
        </div>
        <div style={{ fontSize: 10, color: '#94a3b8', fontStyle: 'italic' }}>
          * 신뢰도는 공간 데이터 매칭 정확도를 의미하며, 인허가 가능 여부의 확정 판단이 아닙니다.
        </div>
      </div>
    );
  }
  const overlays = [
    { label: '지구단위계획',         key: 'isInDistrictUnitPlan' },
    { label: '개발제한구역',         key: 'isDevelopmentRestricted' },
    { label: '개발행위허가제한지역',  key: 'isDevelopmentActionRestricted' },
  ]

  const sec = (title, extra) => ({
    padding: '14px 12px', border: '1px solid #e5e7eb', borderRadius: 10,
    marginBottom: 12, backgroundColor: '#fff', ...extra,
  })

  // ── 관련 검토 항목 정렬 및 파싱 ─────────────────────────────────────
  const sortedRegulations = [...(result?.relatedRegulations || [])].sort((a, b) => {
    const order = { '[제한]': 1, '[계획]': 2, '[용도]': 3, '[밀도]': 4, '[공통]': 5 };
    const getOrder = (s) => {
      for (const key in order) if (s.startsWith(key)) return order[key];
      return 99;
    };
    return getOrder(a) - getOrder(b);
  });

  // ── 텍스트 파일 내보내기 (Export) ──────────────────────────────────
  const handleExport = () => {
    if (!result) return;
    const conf = getConfidence(result);
    const text = `
[건축 법규 1차 검토 결과]

조회 위치: ${lookupInfo?.label || '좌표 지점'}
조회 기준: ${lookupInfo?.mode === 'address' ? '주소 기준' : '좌표 기준'}
신뢰도: ${conf.label} (${conf.explanation})

[1차 판단 요약]
${result.summaryText}

[우선 검토 항목]
${sortedRegulations.length > 0 ? sortedRegulations.map(r => '- ' + r).join('\n') : '없음'}

[상세 레이어 상태]
- 지구단위계획: ${toYesNo(result.overlayFlags?.isInDistrictUnitPlan)}
- 개발제한구역: ${toYesNo(result.overlayFlags?.isDevelopmentRestricted)}
- 개발행위허가제한지역: ${toYesNo(result.overlayFlags?.isDevelopmentActionRestricted)}

[좌표 정보]
WGS84 Lon: ${formatCoord(selectedCoordinate.lon)} / Lat: ${formatCoord(selectedCoordinate.lat)}
분석일시: ${new Date().toLocaleString()}

※ 본 분석은 공공 공간데이터를 기반으로 추출된 1차 참고용 결과입니다.
※ 실제 인허가 가능 여부는 조례 및 지침에 따르므로 반드시 관할 관청에 확인하십시오.
`.trim();

    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `건축법규검토_${lookupInfo?.label?.replace(/\s+/g, '_') || '결과'}.txt`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const isCurrentSaved = savedResults.some(r => r.label === lookupInfo?.label && r.coordinate?.lat === selectedCoordinate?.lat);

  return (
    <aside style={{ width: '100%', boxSizing: 'border-box', color: '#1f2937' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 16 }}>
        <h2 style={{ margin: 0, fontSize: 22, fontWeight: 800, letterSpacing: '-0.025em' }}>
          건축 법규 사전 검토
        </h2>
        <div style={{ display: 'flex', gap: 6 }}>
          {!loading && !error && result && (
            <>
              {/* 세션 저장/비교 기능은 단일 사이트 워크플로우를 위해 일시적으로 숨김 */}
              {/* 
              <button 
                onClick={onSave}
                disabled={isCurrentSaved}
                style={{ 
                  padding: '6px 12px', borderRadius: 8, border: '1px solid #3b82f6', 
                  background: isCurrentSaved ? '#eff6ff' : '#3b82f6', fontSize: 12, fontWeight: 600, 
                  cursor: isCurrentSaved ? 'default' : 'pointer',
                  color: isCurrentSaved ? '#3b82f6' : '#fff'
                }}
              >
                {isCurrentSaved ? '저장됨' : '비교 저장'}
              </button>
              */}
              <button 
                onClick={handleExport}
                style={{ 
                  padding: '8px 16px', borderRadius: 8, border: '1px solid #0284c7', 
                  background: '#0284c7', fontSize: 13, fontWeight: 700, cursor: 'pointer',
                  color: '#fff', boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
                }}
              >
                검토 리포트 (.txt) 저장
              </button>
            </>
          )}
        </div>
      </div>

      {loading && <div style={{ padding: 20, textAlign: 'center', color: '#6b7280' }}>데이터 로드 중...</div>}
      {error && <div style={{ padding: 20, textAlign: 'center', color: '#b91c1c', background: '#fef2f2', borderRadius: 10 }}>조회 실패: 서버 연결을 확인하세요.</div>}
      
      {!loading && !error && !result && (
        <div style={{ padding: 40, textAlign: 'center', color: '#9ca3af', border: '2px dashed #e5e7eb', borderRadius: 12 }}>
          지도를 클릭하거나 주소를 검색하여<br/>조회를 시작하세요.
        </div>
      )}

      {!loading && !error && result && (
        <>
          {/* [1] 핵심 상태 */}
          {getCoreStatus()}

          {/* [2] 조회 기준 및 판정 근거 */}
          <div style={sec('조회 기준', { backgroundColor: '#f8fafc' })}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <div style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600, marginBottom: 2 }}>조회 위치 및 근거</div>
                <div style={{ fontSize: 16, fontWeight: 700, color: '#0f172a' }}>
                  {lookupInfo?.label || (selectedCoordinate ? '선택된 좌표 지점' : '위치 미지정')}
                </div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600, marginBottom: 2 }}>매칭 상태</div>
                <div style={{ fontSize: 11, fontWeight: 600, color: '#3b82f6' }}>
                  {getDiagnosticMessage(result.debug?.reason, result.debug?.nearestDistanceMeters)}
                </div>
              </div>
            </div>
            
            <div style={{ marginTop: 8, paddingTop: 8, borderTop: '1px solid #f1f5f9', fontSize: 12, color: '#64748b' }}>
              <div style={{ display: 'flex', gap: 4, marginBottom: 2 }}>
                <span style={{ fontWeight: 600, color: '#94a3b8', minWidth: 50 }}>입력 방식:</span> 
                <span>{lookupInfo?.mode === 'address' ? '정규화 주소 매칭' : '좌표(WGS84) 직접 지정'}</span>
              </div>
              <div style={{ display: 'flex', gap: 4 }}>
                <span style={{ fontWeight: 600, color: '#94a3b8', minWidth: 50 }}>데이터원:</span> 
                <span style={{ fontSize: 11 }}>{result.meta?.layerName?.split('(')[0] || '공공 공간 데이터'}</span>
              </div>
            </div>
          </div>

          {/* [3] 1차 판단 요약 */}
          <div style={sec('요약')}>
            <h3 style={{ margin: '0 0 10px', fontSize: 13, fontWeight: 700, color: '#475569', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 3, height: 13, background: '#3b82f6', borderRadius: 2 }}></span>
              1차 판단 요약
            </h3>
            <p style={{ margin: 0, fontSize: 14, whiteSpace: 'pre-line', lineHeight: 1.6, color: '#334155' }}>
              {result.summaryText}
            </p>
          </div>

          {/* [4] 우선 검토 항목 (정렬됨) */}
          {sortedRegulations.length > 0 && (
            <div style={sec('검토항목')}>
              <h3 style={{ margin: '0 0 10px', fontSize: 13, fontWeight: 700, color: '#475569', display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ width: 3, height: 13, background: '#f59e0b', borderRadius: 2 }}></span>
                우선 검토 항목
              </h3>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {sortedRegulations.map((reg, i) => {
                  const match = reg.match(/^(\[[^\]]+\])\s*(.*)$/);
                  const prefix = match ? match[1] : '';
                  const body = match ? match[2] : reg;
                  return (
                    <div key={i} style={{ fontSize: 13, lineHeight: 1.5, display: 'flex', gap: 8 }}>
                      {prefix && <span style={{ fontWeight: 700, color: '#d97706', whiteSpace: 'nowrap' }}>{prefix}</span>}
                      <span style={{ color: '#334155' }}>{body}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* [5] 상세 공간 상태 */}
          <div style={sec('상세상태')}>
            <h3 style={{ margin: '0 0 10px', fontSize: 13, fontWeight: 700, color: '#475569', display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 3, height: 13, background: '#10b981', borderRadius: 2 }}></span>
              상세 레이어 상태
            </h3>
            <div style={{ display: 'grid', gap: 6 }}>
              {overlays.map(({ label, key }) => {
                const yn = toYesNo(result?.overlayFlags?.[key])
                const isYes = yn === '예';
                return (
                  <div key={key} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 10px', borderRadius: 8, border: '1px solid #f1f5f9', backgroundColor: isYes ? '#fff1f2' : '#f8fafc' }}>
                    <span style={{ fontSize: 13, color: isYes ? '#991b1b' : '#475569', fontWeight: isYes ? 600 : 400 }}>{label}</span>
                    <span style={{ fontSize: 12, fontWeight: 700, color: isYes ? '#e11d48' : '#64748b' }}>{yn}</span>
                  </div>
                )
              })}
            </div>
          </div>

          {/* [6] 좌표 정보 */}
          <div style={{ padding: '0 12px', marginBottom: 20 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#94a3b8' }}>
              <span>WGS84 Lon: {formatCoord(selectedCoordinate.lon)} / Lat: {formatCoord(selectedCoordinate.lat)}</span>
              <span title={`Feature Count: ${result.meta?.featureCount}`}>Data Ver: {new Date(result.meta?.loadedAt).toLocaleDateString()}</span>
            </div>
          </div>

          {/* [7] 안내 및 분석 범위 */}
          <div style={{ padding: '16px', fontSize: 11, color: '#94a3b8', lineHeight: 1.6, backgroundColor: '#f8fafc', borderRadius: 10, border: '1px solid #f1f5f9', marginBottom: 20 }}>
            <p style={{ margin: '0 0 8px', fontWeight: 700, color: '#64748b' }}>[분석 범위 및 한계]</p>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <p style={{ margin: 0 }}>• 본 분석은 **공공 공간데이터(SHP/CSV)**를 기반으로 추출된 1차 검토 결과입니다.</p>
              <p style={{ margin: 0 }}>• **적용 한계:** 필지 합필/분할, 최근 계획 변경 등 미반영 요소가 있을 수 있으므로 참고용으로만 활용하십시오.</p>
              <p style={{ margin: 0 }}>• **권장 사항:** 실제 인허가 가능 여부는 조례 및 지침에 따르므로 반드시 관할 관청에 확인하십시오.</p>
            </div>
          </div>

          {/* [8] 저장된 검토 목록 (세션) - 단일 사이트 워크플로우를 위해 일시적으로 숨김 */}
          {/* 
          {savedResults.length > 0 && (
            <div style={{ borderTop: '2px solid #f1f5f9', paddingTop: 20 }}>
...
              <div style={{ marginTop: 10, fontSize: 11, color: '#94a3b8', textAlign: 'center' }}>
                목록을 클릭하면 해당 결과를 다시 불러옵니다. (새로고침 시 초기화)
              </div>
            </div>
          )}
          */}
        </>
      )}
    </aside>
  )
}

function StatusBadge({ label, bg, color, border = 'none' }) {
  return (
    <span style={{ 
      padding: '4px 10px', borderRadius: 6, fontSize: 11, fontWeight: 700, 
      backgroundColor: bg, color: color, border: border, whiteSpace: 'nowrap'
    }}>
      {label}
    </span>
  );
}
