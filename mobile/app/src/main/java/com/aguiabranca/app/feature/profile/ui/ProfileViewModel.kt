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
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch
import com.aguiabranca.app.core.network.RefreshBus
import com.aguiabranca.app.core.network.RefreshKeys
import com.aguiabranca.app.core.network.pollingFlow
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
            sessionManager.signOut()
            onDone()
        }
    }
}

/** Top do **mês corrente** (pontos do mês, calculados no servidor). Atualiza a cada 15 s e após criar ideias/concluir projetos. */
@HiltViewModel
class UsersRankingViewModel @Inject constructor(
    private val usersRepo: UsersRepository,
    bus: RefreshBus
) : ViewModel() {
    val ranking: StateFlow<List<User>> = pollingFlow(bus, setOf(RefreshKeys.RANKING)) { usersRepo.topByPointsThisMonth(5) }
        .map { (it as? Outcome.Success)?.value.orEmpty() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())
}
