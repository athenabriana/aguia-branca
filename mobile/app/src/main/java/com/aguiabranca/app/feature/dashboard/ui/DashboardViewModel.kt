package com.aguiabranca.app.feature.dashboard.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.ReportsRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.DashboardState
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Period
import com.aguiabranca.app.core.ui.state.UiState
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.onStart
import kotlinx.coroutines.flow.scan
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.ExperimentalCoroutinesApi
import javax.inject.Inject

/** Dashboard do líder: tudo vem de `/reports/summary`; trocar um filtro refaz a chamada no servidor. */
@OptIn(ExperimentalCoroutinesApi::class)
@HiltViewModel
class DashboardViewModel @Inject constructor(
    reports: ReportsRepository
) : ViewModel() {

    private val _filters = MutableStateFlow(DashboardFilters())
    val filters: StateFlow<DashboardFilters> = _filters.asStateFlow()

    private val _presentation = MutableStateFlow(false)
    val presentation: StateFlow<Boolean> = _presentation.asStateFlow()

    private val retryTick = MutableStateFlow(0)

    val state: StateFlow<UiState<DashboardState>> = combine(_filters, retryTick) { f, _ -> f }
        .flatMapLatest { f ->
            reports.observeSummary(f)
                .map<Outcome<DashboardState>, UiState<DashboardState>> {
                    when (it) {
                        is Outcome.Success -> UiState.Success(it.value)
                        is Outcome.Failure -> UiState.Error(it.error)
                    }
                }
                .onStart { emit(UiState.Loading) }
        }
        // Uma falha passageira do polling não apaga o que já está na tela: mantém o último resumo bom.
        .scan(UiState.Loading as UiState<DashboardState>) { previous, next ->
            if (next is UiState.Error && previous is UiState.Success) previous else next
        }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), UiState.Loading)

    fun setPeriod(p: Period) { _filters.value = _filters.value.copy(period = p) }
    fun setDivision(d: Division?) { _filters.value = _filters.value.copy(division = d) }
    /** "Tentar novamente" após um erro: refaz a chamada com o filtro atual. */
    fun retry() { retryTick.value++ }
    fun togglePresentation() { _presentation.value = !_presentation.value }
}
