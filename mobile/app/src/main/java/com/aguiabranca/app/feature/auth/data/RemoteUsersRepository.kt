package com.aguiabranca.app.feature.auth.data

import com.aguiabranca.app.core.domain.UsersRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.AuthApi
import com.aguiabranca.app.core.network.api.UsersApi
import com.aguiabranca.app.core.network.mapper.toDomain
import com.aguiabranca.app.core.network.pollingFlow
import com.aguiabranca.app.core.network.safeApiCall
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class RemoteUsersRepository @Inject constructor(
    private val authApi: AuthApi,
    private val usersApi: UsersApi,
    private val bus: RefreshBus
) : UsersRepository {

    /** O servidor só expõe o perfil do próprio usuário (`/auth/me`, com pontos e badges atualizados). */
    override fun observe(uid: String): Flow<User?> =
        pollingFlow(bus, setOf(RefreshKeys.ME)) { authApi.me().toDomain() }.map { it.takeIf { u -> u.id == uid } }

    /** Contas são criadas pelo servidor (seed/migração); o app não cria perfis. */
    override suspend fun ensureProfileExists(uid: String, email: String, name: String, role: Role, division: Division): Outcome<Unit> =
        Outcome.Success(Unit)

    /** Ranking do **mês corrente** (soma dos eventos de pontos do mês, calculada no servidor): `points` = pontos do mês. */
    override suspend fun topByPointsThisMonth(limit: Int): Outcome<List<User>> = safeApiCall {
        usersApi.ranking(limit).map { User(id = it.id, name = it.name, email = "", role = Role.OPERADOR, division = Division.CORPORATIVO, points = it.monthPoints) }
    }

    override suspend fun listByRole(role: Role): Outcome<List<User>> = safeApiCall {
        usersApi.list(role.name).map {
            User(
                id = it.id, name = it.name, email = "",
                role = runCatching { Role.valueOf(it.role) }.getOrDefault(role),
                division = runCatching { Division.valueOf(it.division) }.getOrDefault(Division.CORPORATIVO)
            )
        }.sortedBy { it.name.lowercase() }
    }
}
