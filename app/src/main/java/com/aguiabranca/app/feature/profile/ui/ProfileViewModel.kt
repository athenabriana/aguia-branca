package com.aguiabranca.app.feature.profile.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.auth.SessionManager
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.UsersRepository
import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.User
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

data class ProfileUi(
    val user: User? = null,
    val ideasByStatus: Map<IdeaStatus, Int> = emptyMap()
)

@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val usersRepo: UsersRepository,
    private val ideasRepo: IdeasRepository,
    private val sessionManager: SessionManager
) : ViewModel() {
    private val uidFlow = MutableStateFlow<String?>(null)
    fun setUid(uid: String) { uidFlow.value = uid }

    val ui: StateFlow<ProfileUi> = flow {
        uidFlow.collect { uid ->
            if (uid == null) emit(ProfileUi())
            else {
                kotlinx.coroutines.flow.combine(usersRepo.observe(uid), ideasRepo.observeByAuthor(uid)) { user, ideas ->
                    ProfileUi(user = user, ideasByStatus = countByStatus(ideas))
                }.collect { emit(it) }
            }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), ProfileUi())

    private fun countByStatus(ideas: List<Idea>): Map<IdeaStatus, Int> =
        IdeaStatus.entries.associateWith { status -> ideas.count { it.status == status } }

    fun logout(onDone: () -> Unit) {
        viewModelScope.launch {
            runCatching { sessionManager.signOut() }
            onDone()
        }
    }
}

@HiltViewModel
class UsersRankingViewModel @Inject constructor(
    private val usersRepo: UsersRepository
) : ViewModel() {
    private val _ranking = MutableStateFlow<List<User>>(emptyList())
    val ranking: StateFlow<List<User>> = _ranking.asStateFlow()

    init {
        viewModelScope.launch {
            val r = usersRepo.topByPointsThisMonth(5)
            _ranking.value = (r as? Outcome.Success)?.value.orEmpty()
        }
    }
}
