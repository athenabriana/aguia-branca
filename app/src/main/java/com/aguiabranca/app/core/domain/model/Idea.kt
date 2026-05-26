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
    val reviewedAt: Long? = null
)
