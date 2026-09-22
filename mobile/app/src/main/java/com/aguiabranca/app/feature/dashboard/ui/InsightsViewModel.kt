package com.aguiabranca.app.feature.dashboard.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.ReportsRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.Insights
import com.aguiabranca.app.core.ui.state.UiState
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * @param generatedFor filtros usados na geração: se o líder mudar período/divisão depois, o card avisa que o texto é de
 * outro recorte (e oferece gerar de novo) em vez de exibi-lo como se fosse do recorte atual.
 */
data class InsightsUi(val state: UiState<Insights> = UiState.Idle, val generatedFor: DashboardFilters? = null)

@HiltViewModel
class InsightsViewModel @Inject constructor(
    private val reports: ReportsRepository
) : ViewModel() {

    private val _ui = MutableStateFlow(InsightsUi())
    val ui: StateFlow<InsightsUi> = _ui.asStateFlow()

    /**
     * Gera com os filtros ativos do dashboard. `refresh = true` ignora o cache do servidor ("Atualizar").
     * Em qualquer falha (IA fora do ar, resposta inválida, limite) só o erro é exibido — nunca um conteúdo no lugar.
     */
    fun generate(filters: DashboardFilters, refresh: Boolean = false) {
        if (_ui.value.state is UiState.Loading) return // clique duplo: uma geração por vez
        _ui.value = InsightsUi(UiState.Loading, filters)
        viewModelScope.launch {
            _ui.value = when (val outcome = reports.generateInsights(filters, guidelineId = null, refresh = refresh)) {
                is Outcome.Success -> InsightsUi(UiState.Success(outcome.value), filters)
                is Outcome.Failure -> InsightsUi(UiState.Error(outcome.error), filters)
            }
        }
    }

    fun reset() { _ui.value = InsightsUi() }
}
