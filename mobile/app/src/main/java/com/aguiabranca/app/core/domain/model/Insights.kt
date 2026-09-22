package com.aguiabranca.app.core.domain.model

enum class InsightPriority { ALTA, MEDIA, BAIXA }

data class InsightRecommendation(
    val title: String,
    val detail: String,
    val priority: InsightPriority,
    /** Orientação a que a recomendação se refere (já resolvida pelo servidor); `null` quando não se aplica. */
    val relatedGuidelineId: String? = null
)

/** Insights gerados por IA sobre o dashboard (`POST /reports/insights`). */
data class Insights(
    val summary: String,
    val highlights: List<String>,
    val risks: List<String>,
    val recommendations: List<InsightRecommendation>,
    val generatedAt: Long,
    val model: String,
    val fromCache: Boolean
)
