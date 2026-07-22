export const meta = {
  name: 'linesimulator-multi-task',
  description: '작업 규모에 따라 순차/병렬/팀토론을 자동 선택해 실행하고 통합 검토까지 수행',
  phases: [
    { title: '규모 분석' }, { title: '팀 토론' },
    { title: '합의' },      { title: '실행' }, { title: '검토' },
  ],
}

// ── 프로젝트 특화 상수 ───────────────────────────────────────
// WORKTREE_MAP은 반드시 절대경로 (git worktree list가 반환하는 그대로). agent()의
// isolation:'worktree'는 쓰지 않는다 — 격리는 서로 다른 실제 feature 폴더가 제공하고,
// 라우팅은 mkTaskPrompt가 절대경로를 프롬프트에 명시해서 한다.
const WORKTREE_MAP = {
  algorithm: 'C:/Project/3. LineSimulator/TestMcAlgorithm-algorithm',
  modbus:    'C:/Project/3. LineSimulator/TestMcAlgorithm-modbus',
  ui:        'C:/Project/3. LineSimulator/TestMcAlgorithm-ui',
}
const BUILD_CMD = 'dotnet build TestMcAlgorithm.sln -c Debug'

// 기능 → 담당 커스텀 에이전트(.claude/agents/*.md). 기능별로 다른 전문가가 맡으므로
// 단일 WORKER_AGENT_TYPE 대신 이 맵으로 라우팅한다.
const FEATURE_AGENT = {
  algorithm: 'algorithm-engineer',
  modbus:    'modbus-comms-engineer',
  ui:        'wpf-ui-engineer',
}
const agentTypeFor = (task) => FEATURE_AGENT[task.feature] ?? null

// complex 티어 토론용 팀 (관점 다양성). agentType은 .claude/agents/*.md로 해석.
const TEAM = [
  { role: 'Algorithm', agentType: 'algorithm-engineer',      lens: 'MC 선정·임피던스 정확성' },
  { role: 'Protocol',  agentType: 'modbus-comms-engineer',   lens: '레지스터·엔디안·하드웨어 계약' },
  { role: 'UI',        agentType: 'wpf-ui-engineer',         lens: 'MVVM·바인딩·UX' },
  { role: 'Quality',   agentType: 'build-quality-verifier',  lens: '빌드·품질 게이트·회귀' },
]
// 통합 검토 담당.
const REVIEWER_AGENT_TYPE = 'build-quality-verifier'

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    size:   { type: 'string', enum: ['small','medium','large','complex'] },
    reason: { type: 'string' },
    tasks: { type: 'array', items: { type: 'object', properties: {
      name:         { type: 'string' },
      feature:      { type: 'string', enum: Object.keys(WORKTREE_MAP) },
      allowedFiles: { type: 'array', items: { type: 'string' } },
      blockedFiles: { type: 'array', items: { type: 'string' } },
      goal:         { type: 'string' },
    }}}
  }
}
const RESULT_SCHEMA = {
  type: 'object',
  properties: {
    name:          { type: 'string' },
    modifiedFiles: { type: 'array', items: { type: 'string' } },
    buildSuccess:  { type: 'boolean' },
    notes:         { type: 'string' },
  }
}
const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          severity: { type: 'string', enum: ['CRITICAL', 'WARNING', 'INFO'] },
          item:     { type: 'string' },
          file:     { type: 'string' },
          line:     { type: 'integer' },
          detail:   { type: 'string' },
        }
      }
    },
    summary: { type: 'string' },
  }
}

phase('규모 분석')
const plan = await agent(
  `작업 규모와 위험도를 분석해줘.\n작업: ${JSON.stringify(args)}\n\n` +
  `기능(feature) 목록 — 각 작업을 이 중 하나에 배분해야 한다 (다른 값 금지):\n` +
  `${Object.keys(WORKTREE_MAP).map(f => `- ${f}`).join('\n')}\n\n` +
  `규모 기준:\n` +
  `- small:   단일 파일·공유 파일 미포함\n` +
  `- medium:  복수 파일·독립 worktree 분리 가능\n` +
  `- large:   공유 파일 포함·아키텍처 변경·회귀 위험\n` +
  `- complex: 설계 결정 필요·상충 요구사항·시스템 전반 영향`,
  { label: '규모 분석', schema: PLAN_SCHEMA }
)

// 방어 코드: 드물게 size가 별도 필드가 아니라 reason 문자열에 흘러들어오는 경우가
// 관측됨. 최상위 plan.size가 유효하지 않으면 reason에서 복구하고, 실패 시 명확히 알린다.
const VALID_SIZES = ['small', 'medium', 'large', 'complex']
const resolveSize = (p) => {
  if (VALID_SIZES.includes(p.size)) return p.size
  const leaked = String(p.reason || '').match(/size"[^>]*>\s*(small|medium|large|complex)/i)
  if (leaked) return leaked[1].toLowerCase()
  const anyMention = String(p.reason || '').match(/\b(small|medium|large|complex)\b/i)
  if (anyMention) return anyMention[1].toLowerCase()
  throw new Error(`규모 분류 실패 — plan.size가 유효하지 않다: ${JSON.stringify(p)}`)
}
const size = resolveSize(plan)

log(`규모: [${size}] — ${plan.reason}`)

let results = [], review = null

// ── 헬퍼: 실행 프롬프트 생성 ─────────────────────────────────
const mkTaskPrompt = (task, consensus = null) => {
  const dir = WORKTREE_MAP[task.feature]
  return `[${task.name}] ${task.goal}\n` +
    `작업 디렉토리(절대경로 — 반드시 이 안에서만 파일을 읽고 쓰고 커밋하라): ${dir}\n` +
    `이 폴더는 이미 feature/${task.feature} 브랜치로 체크아웃된 별도 git worktree다. ` +
    `저장소 루트 쪽 원본 파일·다른 worktree 폴더는 절대 건드리지 마라.\n` +
    `기존 PowerShell 하네스 게이트를 따르라 (AGENTS.md § Harness): ` +
    `start-task → guard-before-edit → run-quality-gates → complete-task.\n` +
    (consensus ? `합의된 구현 전략:\n${consensus}\n` : '') +
    `수정 허용: ${task.allowedFiles.join(', ')}\n수정 금지: ${task.blockedFiles.join(', ')}\n` +
    `빌드(위 작업 디렉토리에서 실행): ${BUILD_CMD}\n` +
    `1. ${consensus ? '합의 전략 숙지 → ' : ''}작업 디렉토리로 이동 → 2. 코드 분석 → 3. 구현 → 4. 빌드 → 5. 그 디렉토리에서 커밋`
}

// ── 헬퍼: 팬아웃/팬인 병렬 실행 (기능별 담당 에이전트로 라우팅) ────
const fanOut = (tasks, consensus = null) =>
  parallel(tasks.map(task => () =>
    agent(mkTaskPrompt(task, consensus), {
      label: task.name, phase: '실행', schema: RESULT_SCHEMA,
      ...(agentTypeFor(task) ? { agentType: agentTypeFor(task) } : {}),
    })
  ))

// ── 헬퍼: 심층 검증 (생성-검증, build-quality-verifier) ───────
const deepVerify = (res) =>
  agent(
    `[통합 검토] 다음 결과를 심층 검토해줘.\n결과: ${JSON.stringify(res.filter(Boolean))}\n` +
    `발견한 문제는 findings 배열에 severity(CRITICAL/WARNING/INFO)·item·file·line·detail로 ` +
    `담고, summary에 한 줄 총평을 적어줘. 하드웨어 계약(레지스터·엔디안·MC 배정·tolerance) ` +
    `위반은 CRITICAL로 분류하라. 문제가 없으면 findings는 빈 배열로 두고 summary에 승인 취지를 적어줘.`,
    {
      label: 'integration-reviewer', phase: '검토', schema: REVIEW_SCHEMA,
      ...(REVIEWER_AGENT_TYPE ? { agentType: REVIEWER_AGENT_TYPE } : {}),
    }
  )

// ── 전략 디스패치 ─────────────────────────────────────────────
const STRATEGY = {

  // SMALL: 순차 — worktree 라우팅·빌드·커밋은 다른 티어와 동일, 병렬 여부만 다름
  small: async () => {
    phase('실행'); log('소규모 → 순차 실행')
    for (const task of plan.tasks)
      results.push(await agent(mkTaskPrompt(task), {
        label: task.name, phase: '실행', schema: RESULT_SCHEMA,
        ...(agentTypeFor(task) ? { agentType: agentTypeFor(task) } : {}),
      }))
  },

  // MEDIUM: 팬아웃/팬인 + 경량 검토
  medium: async () => {
    phase('실행'); log('중규모 → 병렬 실행')
    results = await fanOut(plan.tasks)
    phase('검토'); log('중규모 → main Claude 인라인 검토')
    review = await agent(
      `작업 결과를 간략히 검토해줘 (빌드·충돌 위주).\n결과: ${JSON.stringify(results.filter(Boolean))}`,
      { label: '검토(main)', phase: '검토' }
    )
  },

  // LARGE: 팬아웃/팬인 + 생성-검증
  large: async () => {
    phase('실행'); log('대규모 → 병렬 실행')
    results = await fanOut(plan.tasks)
    phase('검토'); log('대규모 → integration-reviewer 실행')
    review = await deepVerify(results)
  },

  // COMPLEX: 협의체 → 팬아웃/팬인 + 생성-검증
  complex: async () => {
    phase('팀 토론'); log('복잡한 작업 → Agent Team 토론 시작')
    const opinions = await parallel(TEAM.map(m => () =>
      agent(
        `너는 ${m.role} 전문가야. ${m.lens} 관점에서 아래 작업을 분석하고 구현 방법을 제안해줘.\n` +
        `작업: ${JSON.stringify(plan.tasks)}\n포함: 위험·권장방법·다른관점과의충돌`,
        { label: m.role, phase: '팀 토론', ...(m.agentType ? { agentType: m.agentType } : {}) }
      )
    ))
    phase('합의'); log('토론 완료 → 합의 도출 중')
    const consensus = await agent(
      `${TEAM.length}개 관점을 종합해 최적 구현 전략을 합의해줘.\n` +
      `${opinions.filter(Boolean).map((o,i) => `[${TEAM[i].role}]\n${o}`).join('\n\n')}\n\n` +
      `합의 내용: 1.구현방향 2.각관점제약 3.worktree배분(최종) 4.구현순서`,
      { label: '합의', phase: '합의' }
    )
    log('합의 완료 → 병렬 구현 시작')
    phase('실행'); results = await fanOut(plan.tasks, consensus)
    phase('검토'); log('복잡한 작업 → integration-reviewer 심층 검토')
    review = await deepVerify(results)
  },
}

await STRATEGY[size]()

const succeeded = results.filter(Boolean).filter(r => r.buildSuccess !== false)
const failed    = results.filter(Boolean).filter(r => r.buildSuccess === false)

// review는 large/complex에서만 REVIEW_SCHEMA로 구조화됨 — findings가 있을 때만 CRITICAL 판단.
const criticalFindings = (review && Array.isArray(review.findings))
  ? review.findings.filter(f => f.severity === 'CRITICAL')
  : []

return {
  size,
  succeeded: succeeded.map(r => `✅ ${r.name}`),
  failed:    failed.map(r => `❌ ${r.name}`),
  review,
  mergeBlocked: criticalFindings.length > 0,
  criticalFindings,
}
