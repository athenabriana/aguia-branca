package com.aguiabranca.app.feature.dashboard.data

import com.aguiabranca.app.core.domain.ReportsRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.DashboardState
import com.aguiabranca.app.core.domain.model.Insights
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.api.ReportsApi
import com.aguiabranca.app.core.network.dto.InsightsRequestDto
import com.aguiabranca.app.core.network.mapper.toDomain
import com.aguiabranca.app.core.network.pollingFlow
import com.aguiabranca.app.core.network.safeApiCall
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

/** Dashboard e insights vindos do servidor (líder). Nenhum cálculo de ROI/funil acontece no app. */
@Singleton
class RemoteReportsRepository @Inject constructor(
    private val api: ReportsApi,
    private val bus: RefreshBus
) : ReportsRepository {

    // safeApiCall dentro do polling: uma falha vira Outcome.Failure (a tela decide) e o polling continua no próximo ciclo.
    override fun observeSummary(filters: DashboardFilters): Flow<Outcome<DashboardState>> =
        pollingFlow(bus, setOf(RefreshKeys.REPORTS)) {
            safeApiCall { api.summary(filters.period.name, filters.division?.name).toDomain() }
        }

    override suspend fun generateInsights(filters: DashboardFilters, guidelineId: String?, refresh: Boolean): Outcome<Insights> =
        safeApiCall {
            api.insights(InsightsRequestDto(filters.period.name, filters.division?.name, guidelineId, refresh)).toDomain()
        }
}
