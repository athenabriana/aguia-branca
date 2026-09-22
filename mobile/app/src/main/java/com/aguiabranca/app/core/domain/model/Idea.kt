package com.aguiabranca.app.core.domain.model

data class Idea(
    val id: String,
    val title: String,
    val description: String,
    val category: String,
    val division: Division,
    val guidelineId: String?,
    val authorId: String,
    val authorName: String,
    val status: IdeaStatus,
    val ice: Ice?,
    val reviewerId: String?,
    val reviewComment: String?,
    val createdAt: Long = 0L,
    val reviewedAt: Long? = null,
    /** Título da orientação vinculada; `null` com `guidelineId != null` = orientação removida. */
    val guidelineTitle: String? = null,
    /** Projeto criado a partir desta ideia (alimenta o stepper "Em execução" sem acessar /projects). */
    val linkedProject: LinkedProject? = null
)

data class LinkedProject(val id: String, val stage: ProjectStage, val updatedAt: Long? = null)
