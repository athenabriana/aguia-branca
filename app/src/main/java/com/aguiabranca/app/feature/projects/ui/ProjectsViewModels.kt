package com.aguiabranca.app.feature.projects.ui

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.ProjectInput
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import com.aguiabranca.app.core.domain.usecase.CompleteProjectUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class ProjectsListViewModel @Inject constructor(
    private val repo: ProjectsRepository
) : ViewModel() {
    val projects: StateFlow<List<Project>> = repo.observeAll().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())
}

data class ProjectDetailUi(
    val project: Project? = null,
    val updates: List<ProjectUpdate> = emptyList(),
    val guidelineTitle: String? = null
)

@HiltViewModel
class ProjectDetailViewModel @Inject constructor(
    private val repo: ProjectsRepository,
    private val guidelinesRepo: GuidelinesRepository
) : ViewModel() {
    private val idFlow = MutableStateFlow<String?>(null)
    fun setProjectId(id: String) { idFlow.value = id }

    val ui: StateFlow<ProjectDetailUi> = flow {
        idFlow.collect { id ->
            if (id == null) emit(ProjectDetailUi())
            else combine(repo.observe(id), repo.observeUpdates(id)) { project, updates ->
                val guidelineTitle = project?.guidelineId?.let { gid -> runCatching { guidelinesRepo.observe(gid).first()?.title }.getOrNull() }
                ProjectDetailUi(project = project, updates = updates, guidelineTitle = guidelineTitle)
            }.collect { emit(it) }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), ProjectDetailUi())

    fun delete(id: String, onDone: () -> Unit) {
        viewModelScope.launch {
            repo.delete(id)
            onDone()
        }
    }
}

data class ProjectForm(
    val title: String = "",
    val description: String = "",
    val stage: ProjectStage = ProjectStage.PLANEJAMENTO,
    val statusText: String = "",
    val investment: String = "",
    val financialReturn: String = "",
    val productivityGain: String = "",
    val costReduction: String = "",
    val targetDate: Long? = null,
    val division: Division = Division.CORPORATIVO,
    val guidelineId: String? = null,
    val note: String = "",
    val saving: Boolean = false,
    val error: String? = null
)

@HiltViewModel
class ProjectFormViewModel @Inject constructor(
    private val savedStateHandle: SavedStateHandle,
    private val projectsRepo: ProjectsRepository,
    private val guidelinesRepo: GuidelinesRepository,
    private val completeUseCase: CompleteProjectUseCase
) : ViewModel() {
    private val _form = MutableStateFlow(restore())
    val form: StateFlow<ProjectForm> = _form.asStateFlow()
    val guidelines: StateFlow<List<Guideline>> = guidelinesRepo.observeAll().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    private var editingId: String? = null

    fun load(id: String?) {
        editingId = id
        if (id == null) return
        viewModelScope.launch {
            val p = projectsRepo.observe(id).first() ?: return@launch
            _form.value = ProjectForm(
                title = p.title, description = p.description, stage = p.stage, statusText = p.statusText,
                investment = p.investment.toLong().toString(),
                financialReturn = p.financialReturn.toLong().toString(),
                productivityGain = p.productivityGain.toString(),
                costReduction = p.costReduction.toLong().toString(),
                targetDate = p.targetDate,
                division = p.division,
                guidelineId = p.guidelineId
            )
            persist()
        }
    }

    fun onTitle(v: String) { _form.update { it.copy(title = v) }; persist() }
    fun onDescription(v: String) { _form.update { it.copy(description = v) }; persist() }
    fun onStatusText(v: String) { _form.update { it.copy(statusText = v) }; persist() }
    fun onStage(v: ProjectStage) { _form.update { it.copy(stage = v) }; persist() }
    fun onInvestment(v: String) { _form.update { it.copy(investment = v.filter { c -> c.isDigit() }) }; persist() }
    fun onFinancialReturn(v: String) { _form.update { it.copy(financialReturn = v.filter { c -> c.isDigit() }) }; persist() }
    fun onProductivityGain(v: String) { _form.update { it.copy(productivityGain = v.filter { c -> c.isDigit() || c == '.' || c == ',' }) }; persist() }
    fun onCostReduction(v: String) { _form.update { it.copy(costReduction = v.filter { c -> c.isDigit() }) }; persist() }
    fun onDivision(v: Division) { _form.update { it.copy(division = v) }; persist() }
    fun onGuideline(id: String?) { _form.update { it.copy(guidelineId = id) }; persist() }
    fun onTargetDate(ts: Long?) { _form.update { it.copy(targetDate = ts) }; persist() }
    fun onNote(v: String) { _form.update { it.copy(note = v) } }

    fun submit(managerId: String, managerName: String, onDone: (String) -> Unit) {
        val f = _form.value
        if (f.title.isBlank()) { _form.value = f.copy(error = "Título obrigatório"); return }
        _form.value = f.copy(saving = true, error = null)
        viewModelScope.launch {
            val input = ProjectInput(
                title = f.title, description = f.description, stage = f.stage, statusText = f.statusText,
                investment = f.investment.toDoubleOrNull() ?: 0.0,
                targetDate = f.targetDate,
                financialReturn = f.financialReturn.toDoubleOrNull() ?: 0.0,
                productivityGain = f.productivityGain.replace(',', '.').toDoubleOrNull() ?: 0.0,
                costReduction = f.costReduction.toDoubleOrNull() ?: 0.0,
                division = f.division,
                guidelineId = f.guidelineId
            )
            val id = editingId
            val result = if (id == null) projectsRepo.create(input, managerId, managerName)
            else projectsRepo.update(id, input, managerId, managerName, f.note)
            _form.value = _form.value.copy(saving = false)
            when (result) {
                is Outcome.Success<*> -> {
                    val pid = when (val r = result.value) {
                        is String -> r
                        else -> id ?: ""
                    }
                    if (f.stage == ProjectStage.CONCLUIDO) completeUseCase(pid)
                    onDone(pid)
                }
                is Outcome.Failure -> _form.value = _form.value.copy(error = result.error.toString())
            }
        }
    }

    private fun restore() = ProjectForm(
        title = savedStateHandle.get<String>("p.title") ?: "",
        description = savedStateHandle.get<String>("p.desc") ?: "",
        statusText = savedStateHandle.get<String>("p.statusText") ?: "",
        investment = savedStateHandle.get<String>("p.inv") ?: "",
        financialReturn = savedStateHandle.get<String>("p.ret") ?: "",
        productivityGain = savedStateHandle.get<String>("p.gain") ?: "",
        costReduction = savedStateHandle.get<String>("p.cost") ?: "",
        targetDate = savedStateHandle.get<Long?>("p.date"),
        division = savedStateHandle.get<String>("p.div")?.let { runCatching { Division.valueOf(it) }.getOrNull() } ?: Division.CORPORATIVO,
        stage = savedStateHandle.get<String>("p.stage")?.let { runCatching { ProjectStage.valueOf(it) }.getOrNull() } ?: ProjectStage.PLANEJAMENTO,
        guidelineId = savedStateHandle.get<String>("p.guide")
    )

    private fun persist() {
        val f = _form.value
        savedStateHandle["p.title"] = f.title
        savedStateHandle["p.desc"] = f.description
        savedStateHandle["p.statusText"] = f.statusText
        savedStateHandle["p.inv"] = f.investment
        savedStateHandle["p.ret"] = f.financialReturn
        savedStateHandle["p.gain"] = f.productivityGain
        savedStateHandle["p.cost"] = f.costReduction
        savedStateHandle["p.date"] = f.targetDate
        savedStateHandle["p.div"] = f.division.name
        savedStateHandle["p.stage"] = f.stage.name
        savedStateHandle["p.guide"] = f.guidelineId
    }
}

private fun MutableStateFlow<ProjectForm>.update(transform: (ProjectForm) -> ProjectForm) {
    value = transform(value)
}
