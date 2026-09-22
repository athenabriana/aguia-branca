package com.aguiabranca.app.core.domain.model

data class DashboardFilters(
    val period: Period = Period.ALL,
    val division: Division? = null
)

data class FunnelData(
    val submitted: Int,
    val evaluated: Int,
    val approved: Int,
    val inExecution: Int,
    val roiPositive: Int
)

data class GuidelineImpact(
    val guidelineId: String,
    val title: String,
    val ideasCount: Int,
    val projectsCount: Int,
    val roiPercent: Double?,
    val investment: Double = 0.0,
    val financialReturn: Double = 0.0
)

/** Resumo do dashboard **calculado no servidor** (`GET /reports/summary`): o app só renderiza. */
data class DashboardState(
    val funnel: FunnelData,
    val roiConsolidated: Double?,
    val netProfit: Double,
    val totalInvestment: Double,
    val activeProjects: Int,
    val avgProductivityGain: Double,
    val totalCostReduction: Double,
    /** 6 meses (do mais antigo ao corrente); `null` = mês sem investimento ("—"). */
    val sparklineRoi: List<Float?>,
    val guidelineImpacts: List<GuidelineImpact>,
    /** Já ordenados por ROI decrescente pelo servidor. */
    val projectsByRoi: List<Pair<Project, Double?>>,
    val totalReturn: Double = 0.0,
    val overdueProjects: Int = 0,
    val sparklineMonths: List<String> = emptyList()
)
