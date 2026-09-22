package com.aguiabranca.app.core.network.mapper

import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.FieldChange
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.LinkedProject
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User
import com.aguiabranca.app.core.network.dto.FieldChangeDto
import com.aguiabranca.app.core.network.dto.GuidelineDto
import com.aguiabranca.app.core.network.dto.IdeaDto
import com.aguiabranca.app.core.network.dto.ProjectDto
import com.aguiabranca.app.core.network.dto.ProjectUpdateDto
import com.aguiabranca.app.core.network.dto.UserProfileDto
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.TimeZone

/** Datas ISO-8601 UTC da API ⇄ milissegundos do app (sem `java.time`: o minSdk é 24). */
object Iso {
    private val fraction = Regex("""\.(\d+)""")

    /** `"2026-09-21T22:15:08.47Z"` → millis. `null`/inválida → `null`. */
    fun parse(value: String?): Long? {
        if (value.isNullOrBlank()) return null
        return runCatching {
            val millis = fraction.find(value)?.groupValues?.get(1)?.padEnd(3, '0')?.take(3)?.toLong() ?: 0L
            val base = fraction.replace(value, "").removeSuffix("Z")
            val fmt = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US).apply { timeZone = TimeZone.getTimeZone("UTC") }
            fmt.parse(base)!!.time + millis
        }.getOrNull()
    }

    fun format(millis: Long): String =
        SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US).apply { timeZone = TimeZone.getTimeZone("UTC") }.format(java.util.Date(millis))
}

private inline fun <reified E : Enum<E>> safeValueOf(name: String?, default: E): E =
    name?.let { runCatching { enumValueOf<E>(it) }.getOrNull() } ?: default

fun UserProfileDto.toDomain(): User = User(
    id = id, name = name, email = email,
    role = safeValueOf(role, Role.OPERADOR),
    division = safeValueOf(division, Division.CORPORATIVO),
    points = points.coerceAtLeast(0), badges = badges
)

fun GuidelineDto.toDomain(): Guideline = Guideline(
    id = id, title = title, description = description,
    pillar = safeValueOf(pillar, Pillar.DIRECIONAMENTO),
    authorId = authorId, authorName = authorName, campaign = campaign,
    createdAt = Iso.parse(createdAt) ?: 0L, updatedAt = Iso.parse(updatedAt) ?: 0L
)

fun IdeaDto.toDomain(): Idea = Idea(
    id = id, title = title, description = description, category = category,
    division = safeValueOf(division, Division.CORPORATIVO),
    guidelineId = guidelineId, authorId = authorId, authorName = authorName,
    status = safeValueOf(status, IdeaStatus.SUBMETIDA),
    ice = ice?.let { Ice(it.impact, it.confidence, it.ease) }?.takeIf { it.isComplete },
    reviewerId = reviewerId, reviewComment = reviewComment,
    createdAt = Iso.parse(createdAt) ?: 0L, reviewedAt = Iso.parse(reviewedAt),
    guidelineTitle = guidelineTitle,
    linkedProject = linkedProject?.let { LinkedProject(it.id, safeValueOf(it.stage, ProjectStage.PLANEJAMENTO), Iso.parse(it.updatedAt)) }
)

fun ProjectDto.toDomain(): Project = Project(
    id = id, title = title, description = description,
    stage = safeValueOf(stage, ProjectStage.PLANEJAMENTO), statusText = statusText,
    investment = investment, targetDate = Iso.parse(targetDate),
    financialReturn = financialReturn, productivityGain = productivityGain, costReduction = costReduction,
    division = safeValueOf(division, Division.CORPORATIVO), guidelineId = guidelineId,
    creatorManagerId = creatorManagerId, originatingIdeaId = originatingIdeaId, priorityScore = priorityScore,
    reporterId = reporterId, reporterName = reporterName, responsibleId = responsibleId, responsibleName = responsibleName,
    createdAt = Iso.parse(createdAt) ?: 0L, updatedAt = Iso.parse(updatedAt) ?: 0L, version = version, guidelineTitle = guidelineTitle
)

fun ProjectUpdateDto.toDomain(): ProjectUpdate = ProjectUpdate(
    id = id, authorId = authorId, authorName = authorName, note = note,
    changes = changes.map { it.toDomain() }, createdAt = Iso.parse(createdAt) ?: 0L
)

/**
 * `from`/`to` tipados no JSON → o mesmo formato que o Firestore entregava: número → `Double`, texto → `String`, `null` → `null`.
 * O prazo (`targetDate`) chega como data ISO e vira millis, que é o que a linha do tempo formata.
 */
fun FieldChangeDto.toDomain(): FieldChange = FieldChange(field = field, from = from.toValue(field), to = to.toValue(field))

private fun JsonElement?.toValue(field: String): Any? = when {
    this == null || this is JsonNull -> null
    this is JsonPrimitive && isString -> if (field == "targetDate") Iso.parse(content) ?: content else content
    this is JsonPrimitive -> content.toDoubleOrNull() ?: content
    else -> toString()
}

// ---- Relatórios e IA ----

fun com.aguiabranca.app.core.network.dto.ProjectReportDto.toDomain(): Project = Project(
    id = id, title = title, description = "",
    stage = safeValueOf(stage, ProjectStage.PLANEJAMENTO), statusText = statusText,
    investment = investment, targetDate = Iso.parse(targetDate),
    financialReturn = financialReturn, productivityGain = productivityGain, costReduction = costReduction,
    division = safeValueOf(division, Division.CORPORATIVO), guidelineId = guidelineId,
    creatorManagerId = "", originatingIdeaId = null,
    updatedAt = Iso.parse(updatedAt) ?: 0L, guidelineTitle = guidelineTitle
)

fun com.aguiabranca.app.core.network.dto.ReportSummaryDto.toDomain(): com.aguiabranca.app.core.domain.model.DashboardState =
    com.aguiabranca.app.core.domain.model.DashboardState(
        funnel = com.aguiabranca.app.core.domain.model.FunnelData(funnel.submitted, funnel.evaluated, funnel.approved, funnel.inExecution, funnel.roiPositive),
        roiConsolidated = kpis.roiConsolidated,
        netProfit = kpis.netProfit,
        totalInvestment = kpis.totalInvestment,
        activeProjects = kpis.activeProjects,
        avgProductivityGain = kpis.avgProductivityGain,
        totalCostReduction = kpis.totalCostReduction,
        sparklineRoi = sparkline.map { it.roiPercent?.toFloat() },
        guidelineImpacts = guidelineImpacts.map {
            com.aguiabranca.app.core.domain.model.GuidelineImpact(
                it.guidelineId, it.title, it.ideasCount, it.projectsCount, it.roiPercent, it.investment, it.financialReturn
            )
        },
        projectsByRoi = projects.map { it.toDomain() to it.roiPercent },
        totalReturn = kpis.totalReturn,
        overdueProjects = kpis.overdueProjects,
        sparklineMonths = sparkline.map { it.month }
    )

fun com.aguiabranca.app.core.network.dto.InsightDto.toDomain(): com.aguiabranca.app.core.domain.model.Insights =
    com.aguiabranca.app.core.domain.model.Insights(
        summary = summary, highlights = highlights, risks = risks,
        recommendations = recommendations.map {
            com.aguiabranca.app.core.domain.model.InsightRecommendation(
                it.title, it.detail, safeValueOf(it.priority, com.aguiabranca.app.core.domain.model.InsightPriority.MEDIA), it.relatedGuidelineId
            )
        },
        generatedAt = Iso.parse(generatedAt) ?: 0L, model = model, fromCache = fromCache
    )
