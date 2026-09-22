package com.aguiabranca.app.core.network.dto

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement

@Serializable
data class PagedDto<T>(val items: List<T> = emptyList(), val page: Int = 1, val pageSize: Int = 50, val totalItems: Int = 0) {
    val hasMore: Boolean get() = page * pageSize < totalItems
}

// ---- Orientações ----
@Serializable
data class GuidelineDto(
    val id: String,
    val title: String,
    val description: String = "",
    val pillar: String,
    val campaign: String? = null,
    val authorId: String = "",
    val authorName: String = "",
    val createdAt: String? = null,
    val updatedAt: String? = null
)

@Serializable
data class GuidelineRequestDto(val title: String, val description: String, val pillar: String, val campaign: String? = null)

// ---- Ideias ----
@Serializable data class IceDto(val impact: Int, val confidence: Int, val ease: Int, val score: Int = impact * confidence * ease)
@Serializable data class LinkedProjectDto(val id: String, val stage: String, val updatedAt: String? = null)

@Serializable
data class IdeaDto(
    val id: String,
    val title: String,
    val description: String = "",
    val category: String = "",
    val division: String,
    val guidelineId: String? = null,
    val guidelineTitle: String? = null,
    val authorId: String = "",
    val authorName: String = "",
    val status: String,
    val ice: IceDto? = null,
    val reviewerId: String? = null,
    val reviewComment: String? = null,
    val createdAt: String? = null,
    val updatedAt: String? = null,
    val reviewedAt: String? = null,
    val linkedProject: LinkedProjectDto? = null,
    val pointsAwarded: Int? = null
)

@Serializable
data class IdeaRequestDto(
    val title: String, val description: String, val category: String, val division: String, val guidelineId: String? = null
)

@Serializable data class IceRequestDto(val impact: Int, val confidence: Int, val ease: Int)
@Serializable data class RejectRequestDto(val comment: String)
@Serializable data class ApproveResultDto(val ideaId: String = "", val projectId: String? = null, val alreadyApproved: Boolean = false)

// ---- Projetos ----
@Serializable
data class ProjectDto(
    val id: String,
    val title: String,
    val description: String = "",
    val stage: String,
    val statusText: String = "",
    val investment: Double = 0.0,
    val targetDate: String? = null,
    val financialReturn: Double = 0.0,
    val productivityGain: Double = 0.0,
    val costReduction: Double = 0.0,
    val division: String,
    val guidelineId: String? = null,
    val guidelineTitle: String? = null,
    val creatorManagerId: String = "",
    val originatingIdeaId: String? = null,
    val priorityScore: Int? = null,
    val reporterId: String? = null,
    val reporterName: String? = null,
    val responsibleId: String? = null,
    val responsibleName: String? = null,
    val version: Int = 1,
    val createdAt: String? = null,
    val updatedAt: String? = null
)

@Serializable
data class ProjectRequestDto(
    val title: String,
    val description: String,
    val stage: String,
    val statusText: String,
    val investment: Double,
    val targetDate: String? = null,
    val financialReturn: Double,
    val productivityGain: Double,
    val costReduction: Double,
    val division: String,
    val guidelineId: String? = null,
    val responsibleId: String? = null,
    val note: String? = null,
    val version: Int? = null
)

/** `from`/`to` chegam tipados (número, texto, data ISO ou null); o mapper converte para o modelo de domínio. */
@Serializable data class FieldChangeDto(val field: String, val from: JsonElement? = null, val to: JsonElement? = null)

@Serializable
data class ProjectUpdateDto(
    val id: String,
    val authorId: String = "",
    val authorName: String = "",
    val note: String = "",
    val changes: List<FieldChangeDto> = emptyList(),
    val createdAt: String? = null
)

// ---- Relatórios e IA ----
@Serializable data class FunnelDto(val submitted: Int = 0, val evaluated: Int = 0, val approved: Int = 0, val inExecution: Int = 0, val roiPositive: Int = 0)

@Serializable
data class KpisDto(
    val roiConsolidated: Double? = null,
    val netProfit: Double = 0.0,
    val totalInvestment: Double = 0.0,
    val totalReturn: Double = 0.0,
    val activeProjects: Int = 0,
    val avgProductivityGain: Double = 0.0,
    val totalCostReduction: Double = 0.0,
    val overdueProjects: Int = 0
)

@Serializable data class SparklinePointDto(val month: String, val roiPercent: Double? = null)

@Serializable
data class GuidelineImpactDto(
    val guidelineId: String,
    val title: String,
    val ideasCount: Int = 0,
    val projectsCount: Int = 0,
    val investment: Double = 0.0,
    val financialReturn: Double = 0.0,
    val netProfit: Double = 0.0,
    val roiPercent: Double? = null
)

@Serializable
data class ProjectReportDto(
    val id: String,
    val title: String,
    val stage: String,
    val division: String,
    val guidelineId: String? = null,
    val guidelineTitle: String? = null,
    val investment: Double = 0.0,
    val financialReturn: Double = 0.0,
    val netProfit: Double = 0.0,
    val roiPercent: Double? = null,
    val productivityGain: Double = 0.0,
    val costReduction: Double = 0.0,
    val targetDate: String? = null,
    val daysToDeadline: Int? = null,
    val overdue: Boolean = false,
    val statusText: String = "",
    val updatedAt: String? = null
)

@Serializable
data class ReportSummaryDto(
    val period: String = "ALL",
    val division: String? = null,
    val generatedAt: String? = null,
    val funnel: FunnelDto = FunnelDto(),
    val kpis: KpisDto = KpisDto(),
    val sparkline: List<SparklinePointDto> = emptyList(),
    val guidelineImpacts: List<GuidelineImpactDto> = emptyList(),
    val projects: List<ProjectReportDto> = emptyList()
)

@Serializable
data class InsightsRequestDto(val period: String = "ALL", val division: String? = null, val guidelineId: String? = null, val refresh: Boolean = false)

@Serializable
data class InsightRecommendationDto(val title: String, val detail: String, val priority: String, val relatedGuidelineId: String? = null)

@Serializable
data class InsightDto(
    val summary: String,
    val highlights: List<String> = emptyList(),
    val risks: List<String> = emptyList(),
    val recommendations: List<InsightRecommendationDto> = emptyList(),
    val generatedAt: String? = null,
    val model: String = "",
    val fromCache: Boolean = false
)
