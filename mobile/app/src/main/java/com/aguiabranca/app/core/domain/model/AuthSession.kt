package com.aguiabranca.app.core.domain.model

data class AuthSession(
    val uid: String,
    val name: String,
    val email: String,
    val role: Role,
    val division: Division
) {
    companion object {
        fun from(user: User): AuthSession = AuthSession(
            uid = user.id,
            name = user.name,
            email = user.email,
            role = user.role,
            division = user.division
        )
    }
}
