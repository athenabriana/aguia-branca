package com.aguiabranca.app.core.domain.model

data class User(
    val id: String,
    val name: String,
    val email: String,
    val role: Role,
    val division: Division,
    val points: Int = 0,
    val badges: List<String> = emptyList(),
    val createdAt: Long = 0L
)
