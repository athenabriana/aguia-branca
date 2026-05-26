package com.aguiabranca.app.feature.dashboard.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.ui.components.StageBadge
import com.aguiabranca.app.core.ui.components.StatusBadge
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.stateIn
import javax.inject.Inject

data class DrillDownUi(
    val guideline: Guideline? = null,
    val ideas: List<Idea> = emptyList(),
    val projects: List<Project> = emptyList()
)

@HiltViewModel
class GuidelineDrillDownViewModel @Inject constructor(
    private val guidelinesRepo: GuidelinesRepository,
    private val ideasRepo: IdeasRepository,
    private val projectsRepo: ProjectsRepository
) : ViewModel() {
    private val idFlow = MutableStateFlow<String?>(null)
    fun setGuidelineId(id: String) { idFlow.value = id }

    val ui: StateFlow<DrillDownUi> = flow {
        idFlow.collect { id ->
            if (id == null) emit(DrillDownUi())
            else combine(
                guidelinesRepo.observe(id),
                ideasRepo.observeByGuideline(id),
                projectsRepo.observeByGuideline(id)
            ) { g, ideas, projects ->
                DrillDownUi(guideline = g, ideas = ideas, projects = projects)
            }.collect { emit(it) }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), DrillDownUi())
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GuidelineDrillDownScreen(
    guidelineId: String,
    onBack: () -> Unit,
    onOpenProject: (String) -> Unit,
    vm: GuidelineDrillDownViewModel = hiltViewModel()
) {
    LaunchedEffect(guidelineId) { vm.setGuidelineId(guidelineId) }
    val ui by vm.ui.collectAsState()

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Impacto da orientação") },
                navigationIcon = { TextButton(onClick = onBack) { Text("Voltar") } }
            )
        }
    ) { padding ->
        LazyColumn(modifier = Modifier.fillMaxSize().padding(padding), contentPadding = PaddingValues(16.dp)) {
            ui.guideline?.let { g ->
                item {
                    Surface(shape = RoundedCornerShape(16.dp), color = MaterialTheme.colorScheme.primary) {
                        Column(modifier = Modifier.padding(16.dp).fillMaxWidth()) {
                            Text(
                                g.title,
                                color = androidx.compose.ui.graphics.Color.White,
                                fontWeight = FontWeight.Bold,
                                fontSize = 18.sp
                            )
                            Spacer(Modifier.height(4.dp))
                            Text(g.description, color = androidx.compose.ui.graphics.Color.White.copy(alpha = 0.85f), fontSize = 13.sp)
                        }
                    }
                    Spacer(Modifier.height(16.dp))
                }
            }
            item { Text("Ideias vinculadas (${ui.ideas.size})", fontWeight = FontWeight.SemiBold) }
            items(ui.ideas, key = { it.id }) { idea ->
                Surface(shape = RoundedCornerShape(12.dp), color = MaterialTheme.colorScheme.surface, modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                    Row(modifier = Modifier.fillMaxWidth().padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Text(idea.title, modifier = Modifier.weight(1f))
                        StatusBadge(idea.status)
                    }
                }
            }
            item { Spacer(Modifier.height(16.dp)); Text("Projetos vinculados (${ui.projects.size})", fontWeight = FontWeight.SemiBold) }
            items(ui.projects, key = { it.id }) { p ->
                Surface(
                    shape = RoundedCornerShape(12.dp),
                    color = MaterialTheme.colorScheme.surface,
                    modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp).clickable { onOpenProject(p.id) }
                ) {
                    Row(modifier = Modifier.fillMaxWidth().padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Text(p.title, modifier = Modifier.weight(1f))
                        StageBadge(p.stage)
                    }
                }
            }
        }
    }
}
