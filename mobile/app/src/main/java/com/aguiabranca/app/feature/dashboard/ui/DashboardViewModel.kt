package com.aguiabranca.app.feature.dashboard.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Period
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.feature.dashboard.DashboardComputer
import com.aguiabranca.app.feature.dashboard.DashboardFilters
import com.aguiabranca.app.feature.dashboard.DashboardState
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import javax.inject.Inject

@HiltViewModel
class DashboardViewModel @Inject constructor(
    ideasRepo: IdeasRepository,
    projectsRepo: ProjectsRepository,
    guidelinesRepo: GuidelinesRepository
) : ViewModel() {

    private val _filters = MutableStateFlow(DashboardFilters())
    val filters: StateFlow<DashboardFilters> = _filters.asStateFlow()

    private val _presentation = MutableStateFlow(false)
    val presentation: StateFlow<Boolean> = _presentation.asStateFlow()

    val state: StateFlow<UiState<DashboardState>> = combine(
        ideasRepo.observeAll(),
        projectsRepo.observeAll(),
        guidelinesRepo.observeAll(),
        _filters
    ) { ideas, projects, guidelines, filters ->
        UiState.Success(DashboardComputer.compute(ideas, projects, guidelines, filters)) as UiState<DashboardState>
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), UiState.Loading)

    fun setPeriod(p: Period) { _filters.value = _filters.value.copy(period = p) }
    fun setDivision(d: Division?) { _filters.value = _filters.value.copy(division = d) }
    fun togglePresentation() { _presentation.value = !_presentation.value }
}
