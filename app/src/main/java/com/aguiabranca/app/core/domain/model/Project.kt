package com.aguiabranca.app.core.domain.model

data class Project(
    val id: String,
    val title: String,
    val description: String,
    val stage: ProjectStage,
    val statusText: String,
    val investment: Double,
    val targetDate: Long?,
    val financialReturn: Double,
    val productivityGain: Double,
    val costReduction: Double,
    val division: Division,
    val guidelineId: String?,
    val creatorManagerId: String,
    val originatingIdeaId: String?,
    val priorityScore: Int? = null,
    val createdAt: Long = 0L,
    val updatedAt: Long = 0L
)

data class ProjectUpdate(
    val id: String,
    val authorId: String,
    val authorName: String,
    val note: String,
    val changes: List<FieldChange>,
    val createdAt: Long = 0L
)

data class FieldChange(
    val field: String,
    val from: Any?,
    val to: Any?
)
