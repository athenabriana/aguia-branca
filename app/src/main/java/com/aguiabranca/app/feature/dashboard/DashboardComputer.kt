package com.aguiabranca.app.feature.dashboard

import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Period
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectStage
import java.util.Calendar

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
    val roiPercent: Double?
)

data class DashboardState(
    val funnel: FunnelData,
    val roiConsolidated: Double?,
    val netProfit: Double,
    val totalInvestment: Double,
    val activeProjects: Int,
    val avgProductivityGain: Double,
    val totalCostReduction: Double,
    val sparklineRoi: List<Float?>,
    val guidelineImpacts: List<GuidelineImpact>,
    val projectsByRoi: List<Pair<Project, Double?>>
)

object DashboardComputer {

    fun compute(
        ideas: List<Idea>,
        projects: List<Project>,
        guidelines: List<Guideline>,
        filters: DashboardFilters
    ): DashboardState {
        val (periodStart, periodEnd) = periodWindow(filters.period)

        val filteredIdeas = ideas.filter { idea ->
            (filters.division == null || idea.division == filters.division) &&
                inWindow(idea.createdAt, periodStart, periodEnd)
        }
        val filteredProjects = projects.filter { p ->
            (filters.division == null || p.division == filters.division) &&
                inWindow(p.updatedAt, periodStart, periodEnd)
        }

        val funnel = FunnelData(
            submitted = filteredIdeas.size,
            evaluated = filteredIdeas.count { it.status in evaluatedStatuses },
            approved = filteredIdeas.count { it.status in approvedStatuses },
            inExecution = filteredProjects.count { it.stage in inExecutionStages },
            roiPositive = filteredProjects.count { it.stage == ProjectStage.CONCLUIDO && it.financialReturn > it.investment }
        )

        val totalInvestment = filteredProjects.sumOf { it.investment }
        val totalReturn = filteredProjects.sumOf { it.financialReturn }
        val netProfit = totalReturn - totalInvestment
        val roiConsolidated = if (totalInvestment > 0) (netProfit / totalInvestment) * 100.0 else null

        val active = filteredProjects.count { it.stage == ProjectStage.EM_EXECUCAO }
        val avgGain = filteredProjects.filter { it.productivityGain > 0 }.let { list ->
            if (list.isEmpty()) 0.0 else list.sumOf { it.productivityGain } / list.size
        }
        val totalCostReduction = filteredProjects.sumOf { it.costReduction }

        val sparkProjects = projects.filter { p -> filters.division == null || p.division == filters.division }
        val sparkline = buildSparkline(sparkProjects)

        val impactByGuideline = guidelines.map { g ->
            val gIdeas = filteredIdeas.filter { it.guidelineId == g.id }
            val gProjects = filteredProjects.filter { it.guidelineId == g.id }
            val gInv = gProjects.sumOf { it.investment }
            val gRet = gProjects.sumOf { it.financialReturn }
            val gRoi = if (gInv > 0) ((gRet - gInv) / gInv) * 100.0 else null
            GuidelineImpact(
                guidelineId = g.id,
                title = g.title,
                ideasCount = gIdeas.size,
                projectsCount = gProjects.size,
                roiPercent = gRoi
            )
        }.sortedWith(compareByDescending<GuidelineImpact> { it.projectsCount > 0 }.thenByDescending { it.roiPercent ?: Double.NEGATIVE_INFINITY })

        val projectsByRoi = filteredProjects.map { p ->
            val roi = if (p.investment > 0) ((p.financialReturn - p.investment) / p.investment) * 100.0 else null
            p to roi
        }.sortedByDescending { it.second ?: Double.NEGATIVE_INFINITY }

        return DashboardState(
            funnel = funnel,
            roiConsolidated = roiConsolidated,
            netProfit = netProfit,
            totalInvestment = totalInvestment,
            activeProjects = active,
            avgProductivityGain = avgGain,
            totalCostReduction = totalCostReduction,
            sparklineRoi = sparkline,
            guidelineImpacts = impactByGuideline,
            projectsByRoi = projectsByRoi
        )
    }

    private val evaluatedStatuses = setOf(IdeaStatus.EM_ANALISE, IdeaStatus.APROVADA, IdeaStatus.REJEITADA, IdeaStatus.IMPLEMENTADA)
    private val approvedStatuses = setOf(IdeaStatus.APROVADA, IdeaStatus.IMPLEMENTADA)
    private val inExecutionStages = setOf(ProjectStage.EM_EXECUCAO, ProjectStage.CONCLUIDO)

    private fun periodWindow(period: Period): Pair<Long, Long> {
        if (period == Period.ALL) return 0L to Long.MAX_VALUE
        val cal = Calendar.getInstance()
        val end = cal.timeInMillis
        when (period) {
            Period.THIS_MONTH -> {
                cal.set(Calendar.DAY_OF_MONTH, 1); zeroTime(cal)
            }
            Period.LAST_QUARTER -> {
                cal.add(Calendar.MONTH, -3); zeroTime(cal)
            }
            Period.THIS_YEAR -> {
                cal.set(Calendar.DAY_OF_YEAR, 1); zeroTime(cal)
            }
            Period.ALL -> {}
        }
        return cal.timeInMillis to end
    }

    private fun zeroTime(c: Calendar) {
        c.set(Calendar.HOUR_OF_DAY, 0); c.set(Calendar.MINUTE, 0); c.set(Calendar.SECOND, 0); c.set(Calendar.MILLISECOND, 0)
    }

    private fun inWindow(ts: Long, start: Long, end: Long): Boolean = ts in start..end || (start == 0L && end == Long.MAX_VALUE)

    private fun buildSparkline(projects: List<Project>): List<Float?> {
        val buckets = (0..5).map { offset ->
            val cal = Calendar.getInstance()
            cal.add(Calendar.MONTH, -offset)
            zeroTime(cal); cal.set(Calendar.DAY_OF_MONTH, 1)
            val start = cal.timeInMillis
            cal.add(Calendar.MONTH, 1)
            val end = cal.timeInMillis
            val inBucket = projects.filter { it.updatedAt in start until end }
            val inv = inBucket.sumOf { it.investment }
            val ret = inBucket.sumOf { it.financialReturn }
            if (inv <= 0.0) null else (((ret - inv) / inv) * 100.0).toFloat()
        }
        return buckets.reversed()
    }
}
