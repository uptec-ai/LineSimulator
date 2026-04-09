# TestMcAlgorithm

이 프로젝트는 조합표를 직접 하드코딩하지 않고, `선로 family 개수 계산 -> 실제 MC 배정 -> 남은 shared MC로 BUS2/BUS3 선택` 흐름으로 구성했습니다.

1. `Zgrid = 380^2 / (Srated × SCR)` 로 목표 임피던스를 계산합니다.
2. `BUS1`은 아래 family 조합으로만 계산합니다.
   - `A = 1444선로 -> MC1, MC2, MC3, MC5, MC6`
   - `B = 825선로 -> MC4, MC7`
   - `C = 962선로 -> MC8`
   - `D = 577선로 -> MC9`
3. `A/B/C/D` 개수 조합을 모두 시도해서 허용오차(`±0.3mΩ`) 안에 들어오는 BUS1 후보를 찾습니다.
4. family 개수가 정해지면 BUS1은 아래 우선순위로 실제 MC 번호를 배정합니다.
   - 1순위: `MC2, MC4, MC5, MC6, MC7`
   - 2순위: 공유 MC `MC1, MC3, MC8, MC9`
   - `A` family는 `MC2 -> MC5 -> MC6 -> MC1 -> MC3` 순서로 사용합니다.
5. 이후 shared pool `MC1, MC3, MC8, MC9, MC10`에서 BUS1이 이미 사용한 MC를 제외합니다.
6. 남은 shared pool로 `BUS2`, 그 다음 `BUS3`를 찾습니다.
7. `BUS3`는 `BUS2`가 있을 때만 허용합니다.
8. `SCR 투입 실행`을 누르면 선택된 MC를 `낮은 번호 -> 높은 번호` 순으로 `1초 간격`으로 켭니다.

현재 버전은 `MC1~MC10`만 알고리즘 대상이고, `MC11~MC19`는 수동 조작용 reserve로 두었습니다.
