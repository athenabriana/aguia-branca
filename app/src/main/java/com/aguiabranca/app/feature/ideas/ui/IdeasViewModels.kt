package com.aguiabranca.app.feature.ideas.ui

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.CreateIdeaInput
import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.UpdateIdeaInput
import com.aguiabranca.app.core.domain.error.DomainError
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.usecase.ApproveIdeaUseCase
import com.aguiabranca.app.core.domain.usecase.RejectIdeaUseCase
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.core.util.Analytics
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

data class NewIdeaForm(
    val title: String = "",
    val description: String = "",
    val category: String = "",
    val division: Division = Division.CORPORATIVO,
    val guidelineId: String? = null,
    val saving: Boolean = false,
    val error: String? = null,
    val toast: String? = null
)

@HiltViewModel
class NewIdeaViewModel @Inject constructor(
    private val savedStateHandle: SavedStateHandle,
    private val repo: IdeasRepository,
    private val guidelinesRepo: GuidelinesRepository,
    private val analytics: Analytics
) : ViewModel() {
    private val _form = MutableStateFlow(
        NewIdeaForm(
            title = savedStateHandle.get<String>(K_T) ?: "",
            description = savedStateHandle.get<String>(K_D) ?: "",
            category = savedStateHandle.get<String>(K_C) ?: "",
            division = savedStateHandle.get<String>(K_DIV)?.let { runCatching { Division.valueOf(it) }.getOrNull() } ?: Division.CORPORATIVO,
            guidelineId = savedStateHandle.get<String>(K_G)
        )
    )
    val form: StateFlow<NewIdeaForm> = _form.asStateFlow()

    val guidelines: StateFlow<List<Guideline>> = guidelinesRepo.observeAll().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    fun init(defaultDivision: Division) {
        if (savedStateHandle.get<String>(K_DIV) == null) {
            _form.value = _form.value.copy(division = defaultDivision)
            savedStateHandle[K_DIV] = defaultDivision.name
        }
    }
    fun onTitle(v: String) { _form.value = _form.value.copy(title = v); savedStateHandle[K_T] = v }
    fun onDesc(v: String) { _form.value = _form.value.copy(description = v); savedStateHandle[K_D] = v }
    fun onCategory(v: String) { _form.value = _form.value.copy(category = v); savedStateHandle[K_C] = v }
    fun onDivision(v: Division) { _form.value = _form.value.copy(division = v); savedStateHandle[K_DIV] = v.name }
    fun onGuideline(id: String?) { _form.value = _form.value.copy(guidelineId = id); savedStateHandle[K_G] = id }

    suspend fun searchCategoryPrefix(prefix: String): List<String> = repo.searchCategoriesByPrefix(prefix).first()

    fun submit(authorId: String, authorName: String, onSuccess: (toast: String) -> Unit) {
        val f = _form.value
        if (f.title.isBlank() || f.description.isBlank() || f.category.length < 2) {
            _form.value = f.copy(error = "Preencha título, descrição e categoria (≥ 2 chars)")
            return
        }
        _form.value = f.copy(saving = true, error = null)
        viewModelScope.launch {
            val result = repo.createIdea(
                CreateIdeaInput(
                    title = f.title, description = f.description, category = f.category,
                    division = f.division, guidelineId = f.guidelineId,
                    authorId = authorId, authorName = authorName
                )
            )
            _form.value = _form.value.copy(saving = false)
            when (result) {
                is Outcome.Success -> {
                    analytics.logIdeaCreated(hasGuideline = f.guidelineId != null)
                    val toast = if (f.guidelineId != null) "+15 pts (10 base + 5 conexão estratégica)" else "+10 pts"
                    onSuccess(toast)
                }
                is Outcome.Failure -> _form.value = _form.value.copy(error = describeError(result.error))
            }
        }
    }

    private companion object {
        const val K_T = "newidea.title"
        const val K_D = "newidea.desc"
        const val K_C = "newidea.cat"
        const val K_DIV = "newidea.div"
        const val K_G = "newidea.guide"
    }
}

@HiltViewModel
class MyIdeasViewModel @Inject constructor(
    private val repo: IdeasRepository
) : ViewModel() {
    private val authorIdFlow = MutableStateFlow<String?>(null)
    fun setAuthor(uid: String) { authorIdFlow.value = uid }

    val ideas: StateFlow<List<Idea>> = flow {
        authorIdFlow.collect { uid ->
            if (uid == null) emit(emptyList())
            else repo.observeByAuthor(uid).collect { emit(it) }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())
}

@HiltViewModel
class CurationViewModel @Inject constructor(
    private val repo: IdeasRepository,
    private val approveUseCase: ApproveIdeaUseCase,
    private val rejectUseCase: RejectIdeaUseCase
) : ViewModel() {
    val ideas: StateFlow<List<Idea>> = repo.observeForCuration()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    fun sorted(list: List<Idea>): List<Idea> {
        val (withIce, withoutIce) = list.partition { it.ice != null }
        return withIce.sortedByDescending { it.ice?.score ?: 0 } + withoutIce
    }
}

data class IdeaDetailUi(
    val idea: Idea? = null,
    val guideline: Guideline? = null,
    val saving: Boolean = false,
    val error: String? = null
)

@HiltViewModel
class IdeaDetailViewModel @Inject constructor(
    private val ideasRepo: IdeasRepository,
    private val guidelinesRepo: GuidelinesRepository,
    private val approveUseCase: ApproveIdeaUseCase,
    private val rejectUseCase: RejectIdeaUseCase
) : ViewModel() {

    private val ideaIdFlow = MutableStateFlow<String?>(null)
    fun setIdeaId(id: String) { ideaIdFlow.value = id }

    val ui: StateFlow<UiState<IdeaDetailUi>> = flow {
        emit(UiState.Loading)
        ideaIdFlow.collect { id ->
            if (id == null) emit(UiState.Idle) else {
                ideasRepo.observe(id).collect { idea ->
                    if (idea == null) emit(UiState.Error(DomainError.NotFound("ideia", id)))
                    else {
                        val guideline = idea.guidelineId?.let { gid -> runCatching { guidelinesRepo.observe(gid).first() }.getOrNull() }
                        emit(UiState.Success(IdeaDetailUi(idea = idea, guideline = guideline)))
                    }
                }
            }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), UiState.Idle)

    fun saveIce(id: String, ice: Ice, reviewerId: String, onDone: () -> Unit) {
        viewModelScope.launch {
            ideasRepo.saveIce(id, ice, reviewerId)
            onDone()
        }
    }

    fun approve(id: String, reviewerId: String, reviewerName: String, onDone: (projectId: String?) -> Unit) {
        viewModelScope.launch {
            val r = approveUseCase(id, reviewerId, reviewerName)
            onDone((r as? Outcome.Success)?.value)
        }
    }

    fun reject(id: String, reviewerId: String, comment: String, onDone: () -> Unit) {
        viewModelScope.launch {
            rejectUseCase(id, reviewerId, comment)
            onDone()
        }
    }

    fun delete(id: String, authorId: String, onDone: () -> Unit) {
        viewModelScope.launch {
            ideasRepo.deleteIdea(id, authorId)
            onDone()
        }
    }
}

internal fun describeError(error: DomainError): String = when (error) {
    is DomainError.NetworkUnavailable -> "Sem conexão."
    is DomainError.ValidationFailed -> "${error.field}: ${error.reason}"
    is DomainError.NotFound -> "Não encontrado."
    is DomainError.PermissionDenied -> error.message ?: "Permissão negada."
    is DomainError.ConflictingState -> error.message
    is DomainError.NotAuthenticated -> error.message ?: "Faça login."
    is DomainError.Unknown -> error.cause?.localizedMessage ?: "Erro."
}
