package com.aguiabranca.app.core.domain.usecase

import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.IdeasApi
import com.aguiabranca.app.core.network.safeApiCall
import com.aguiabranca.app.core.util.Analytics
import javax.inject.Inject

/**
 * Aprovação é regra do servidor (`POST /ideas/{id}/approve`): cria o projeto rascunho, credita +50 ao autor e é idempotente.
 * O app só dispara a chamada. Auto-aprovação volta como `403 SELF_APPROVAL_FORBIDDEN` (mensagem do servidor).
 * Devolve o id do projeto criado.
 */
class ApproveIdeaUseCase @Inject constructor(
    private val api: IdeasApi,
    private val analytics: Analytics,
    private val bus: RefreshBus
) {
    @Suppress("UNUSED_PARAMETER")
    suspend operator fun invoke(ideaId: String, reviewerId: String, reviewerName: String): Outcome<String> {
        val outcome = safeApiCall { api.approve(ideaId).projectId.orEmpty() }
        if (outcome is Outcome.Success) {
            analytics.logIdeaApproved(outcome.value)
            bus.invalidate(RefreshKeys.IDEAS, RefreshKeys.PROJECTS, RefreshKeys.REPORTS)
        }
        return outcome
    }
}
