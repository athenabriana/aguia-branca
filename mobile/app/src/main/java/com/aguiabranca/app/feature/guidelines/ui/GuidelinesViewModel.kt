package com.aguiabranca.app.feature.guidelines.ui

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Pillar
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class GuidelinesViewModel @Inject constructor(
    private val repo: GuidelinesRepository
) : ViewModel() {
    val guidelines: StateFlow<List<Guideline>> = repo.observeAll().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    fun delete(id: String, onDone: () -> Unit = {}) {
        viewModelScope.launch {
            repo.delete(id)
            onDone()
        }
    }
}

data class GuidelineEditForm(
    val title: String = "",
    val description: String = "",
    val pillar: Pillar = Pillar.DIRECIONAMENTO,
    val saving: Boolean = false,
    val error: String? = null
)

@HiltViewModel
class GuidelinesAdminViewModel @Inject constructor(
    private val savedStateHandle: SavedStateHandle,
    private val repo: GuidelinesRepository
) : ViewModel() {

    private val _form = MutableStateFlow(
        GuidelineEditForm(
            title = savedStateHandle.get<String>(KEY_TITLE) ?: "",
            description = savedStateHandle.get<String>(KEY_DESC) ?: "",
            pillar = savedStateHandle.get<String>(KEY_PILLAR)?.let { runCatching { Pillar.valueOf(it) }.getOrNull() } ?: Pillar.DIRECIONAMENTO
        )
    )
    val form: StateFlow<GuidelineEditForm> = _form.asStateFlow()

    private var editingId: String? = null

    fun load(id: String?) {
        editingId = id
        if (id == null) return
        viewModelScope.launch {
            val g = repo.observe(id).first()
            if (g != null) {
                _form.value = GuidelineEditForm(title = g.title, description = g.description, pillar = g.pillar)
                savedStateHandle[KEY_TITLE] = g.title
                savedStateHandle[KEY_DESC] = g.description
                savedStateHandle[KEY_PILLAR] = g.pillar.name
            }
        }
    }

    fun onTitle(v: String) { _form.value = _form.value.copy(title = v); savedStateHandle[KEY_TITLE] = v }
    fun onDescription(v: String) { _form.value = _form.value.copy(description = v); savedStateHandle[KEY_DESC] = v }
    fun onPillar(p: Pillar) { _form.value = _form.value.copy(pillar = p); savedStateHandle[KEY_PILLAR] = p.name }

    fun save(authorId: String, authorName: String, onDone: () -> Unit) {
        val f = _form.value
        if (f.title.isBlank()) { _form.value = f.copy(error = "Título obrigatório"); return }
        _form.value = f.copy(saving = true, error = null)
        viewModelScope.launch {
            val id = editingId
            val outcome = if (id == null) repo.create(f.title, f.description, f.pillar, authorId, authorName)
            else repo.update(id, f.title, f.description, f.pillar)
            _form.value = _form.value.copy(saving = false)
            if (outcome is com.aguiabranca.app.core.domain.error.Outcome.Failure) {
                _form.value = _form.value.copy(error = outcome.error.toString())
            } else {
                onDone()
            }
        }
    }

    private companion object {
        const val KEY_TITLE = "guideline.title"
        const val KEY_DESC = "guideline.description"
        const val KEY_PILLAR = "guideline.pillar"
    }
}
