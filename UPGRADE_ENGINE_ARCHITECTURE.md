# Upgrade Engine Architecture

## Goal Shift

- `용도 추가` 중심 확장에서 `전 용도 확장 가능한 공통 엔진 설계`로 목표를 전환한다.
- 기존 개별 용도 분기 구현은 점진적으로 `UseProfile` 기반 구조로 이전한다.
- 현재 지원 용도는 완성형 기능 단위가 아니라 generalized engine의 샘플셋으로 본다.

## Fixed Layers

다음 9개 레이어를 고정 구조로 유지한다.

1. `Spatial Layer`
2. `Profile Layer`
3. `Calculation Layer`
4. `Rule Layer`
5. `Task Layer`
6. `Checklist Layer`
7. `Manual Review Layer`
8. `Ordinance Layer`
9. `AI Assist Layer`

## Fixed I/O Stack

모든 검토 흐름은 다음 5계층 입출력 프레임을 따른다.

1. `Query`
2. `RawInputs`
3. `DerivedInputs`
4. `PlanningContext`
5. `Output`

## Layer Responsibilities

- `Spatial Layer`: 주소, 좌표, 필지, 용도지역, overlay, 도시계획시설, 개발행위허가 여부 처리
- `Profile Layer`: `selectedUse`를 내부 `UseProfile`로 변환
- `Calculation Layer`: 서버 단일 권위 계산 수행
- `Rule Layer`: `RuleBundle` 조합으로 규칙 적용
- `Task Layer`: 규칙 결과를 표준 `Task`로 집계
- `Checklist Layer`: 프로젝트 수준 요약 생성
- `Manual Review Layer`: 자동판정 불가 항목 구조화
- `Ordinance Layer`: 도시군별 조례 메타데이터 및 확인 항목 관리
- `AI Assist Layer`: 검색/탐색 보조만 수행

## Migration Note

- 기존 `/review`, `reviewItems`, `law-layers` 응답은 당분간 유지한다.
- 신규 타입은 공통 엔진 전환을 위한 기반 구조로 먼저 추가한다.
- 기존 하드코딩 용도 검증은 `UseProfileRegistry`를 기준으로 교체한다.

## Current Sample Set

- 현재 서버에 연결된 샘플셋은 `공동주택`, `제1종근린생활시설`, `제2종근린생활시설`, `업무시설`이다.
- 이후 `교육시설`, `의료시설`, `숙박시설`, `공장`, `창고`, `물류시설`은 같은 `UseProfile` 구조로 확장한다.
- 샘플셋은 개별 기능 단위가 아니라 generalized engine 검증용 대표 용도군으로 취급한다.
