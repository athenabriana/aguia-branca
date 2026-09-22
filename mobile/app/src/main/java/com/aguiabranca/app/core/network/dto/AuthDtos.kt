package com.aguiabranca.app.core.network.dto

import kotlinx.serialization.Serializable

@Serializable data class LoginRequestDto(val email: String, val password: String)
@Serializable data class RefreshRequestDto(val refreshToken: String)
@Serializable data class LogoutRequestDto(val refreshToken: String)

@Serializable
data class UserProfileDto(
    val id: String,
    val name: String,
    val email: String = "",
    val role: String,
    val division: String,
    val points: Int = 0,
    val badges: List<String> = emptyList()
)

@Serializable
data class AuthResponseDto(
    val accessToken: String,
    val refreshToken: String,
    val expiresIn: Int = 0,
    val user: UserProfileDto
)

@Serializable data class UserSummaryDto(val id: String, val name: String, val role: String, val division: String)
@Serializable data class RankingEntryDto(val id: String, val name: String, val monthPoints: Int = 0)

/** `application/problem+json` da API: `code` estável + `errors[]` por campo. */
@Serializable
data class ProblemDetailsDto(
    val status: Int? = null,
    val code: String? = null,
    val title: String? = null,
    val detail: String? = null,
    val errors: List<ProblemErrorDto> = emptyList()
)

@Serializable data class ProblemErrorDto(val code: String? = null, val message: String? = null, val field: String? = null)
