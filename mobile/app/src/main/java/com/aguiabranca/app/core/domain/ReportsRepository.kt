package com.aguiabranca.app.core.domain

import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.DashboardState
import com.aguiabranca.app.core.domain.model.Insights
import kotlinx.coroutines.flow.Flow

interface ReportsRepository {
    /** Emite o resumo agora, a cada 15 s e após qualquer escrita que o afete. Falhas chegam como `Outcome.Failure`. */
    fun observeSummary(filters: DashboardFilters): Flow<Outcome<DashboardState>>

    /**
     * Gera insights de IA para os filtros (e, opcionalmente, uma orientação). `refresh = true` ignora o cache do servidor.
     * Falha da IA (503/502/429) volta como `Outcome.Failure` — nunca se fabrica conteúdo.
     */
    suspend fun generateInsights(filters: DashboardFilters, guidelineId: String? = null, refresh: Boolean = false): Outcome<Insights>
}
