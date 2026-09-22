package com.aguiabranca.app.core.domain

import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User
import kotlinx.coroutines.flow.Flow

interface UsersRepository {
    fun observe(uid: String): Flow<User?>
    suspend fun ensureProfileExists(
        uid: String,
        email: String,
        name: String,
        role: Role,
        division: Division
    ): Outcome<Unit>
    suspend fun topByPointsThisMonth(limit: Int): Outcome<List<User>>
    suspend fun listByRole(role: Role): Outcome<List<User>>
}
