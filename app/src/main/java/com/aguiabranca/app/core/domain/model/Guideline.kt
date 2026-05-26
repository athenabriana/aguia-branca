package com.aguiabranca.app.core.domain.model

data class Guideline(
    val id: String,
    val title: String,
    val description: String,
    val pillar: Pillar,
    val authorId: String,
    val authorName: String,
    val createdAt: Long = 0L,
    val updatedAt: Long = 0L
)
