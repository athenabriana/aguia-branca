package com.aguiabranca.app.feature.ideas.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.components.GuidelineBadge
import com.aguiabranca.app.core.ui.components.IceMatrix
import com.aguiabranca.app.core.ui.components.JourneyStepper
import com.aguiabranca.app.core.ui.components.JourneyStage
import com.aguiabranca.app.core.ui.components.StatusBadge
import com.aguiabranca.app.core.ui.local.LocalSession
import com.aguiabranca.app.core.ui.state.UiState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun IdeaDetailScreen(
    ideaId: String,
    onBack: () -> Unit,
    vm: IdeaDetailViewModel = hiltViewModel()
) {
    LaunchedEffect(ideaId) { vm.setIdeaId(ideaId) }
    val session = LocalSession.current ?: return
    val state by vm.ui.collectAsState()
    var rejectComment by remember { mutableStateOf("") }
    var showReject by remember { mutableStateOf(false) }
    var showDelete by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Detalhe da ideia") },
                navigationIcon = { TextButton(onClick = onBack) { Text("Voltar") } }
            )
        }
    ) { padding ->
        when (val s = state) {
            is UiState.Success -> {
                val idea = s.data.idea ?: return@Scaffold
                Column(
                    modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp).verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    if (idea.guidelineId != null) {
                        GuidelineBadge(title = s.data.guideline?.title)
                    } else {
                        Text("Sem orientação vinculada", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp)
                    }
                    Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                        Text(idea.title, fontWeight = FontWeight.Bold, fontSize = 20.sp, modifier = Modifier.weight(1f))
                        StatusBadge(idea.status)
                    }
                    Text(idea.description)
                    Text("Categoria: ${idea.category}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp)
                    Text("Divisão: ${divisionLabel(idea.division)}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp)

                    val isAuthor = session.uid == idea.authorId && session.role == Role.OPERADOR
                    val canCurate = session.role == Role.GESTOR && idea.status in setOf(IdeaStatus.SUBMETIDA, IdeaStatus.EM_ANALISE)

                    if (isAuthor) {
                        Spacer(Modifier.height(8.dp))
                        val current = when (idea.status) {
                            IdeaStatus.SUBMETIDA -> JourneyStage.SUBMETIDA
                            IdeaStatus.EM_ANALISE -> JourneyStage.EM_ANALISE
                            IdeaStatus.APROVADA -> JourneyStage.APROVADA
                            IdeaStatus.IMPLEMENTADA -> JourneyStage.RESULTADO
                            IdeaStatus.REJEITADA -> null
                        }
                        JourneyStepper(
                            currentStage = current,
                            dates = mapOf(
                                JourneyStage.SUBMETIDA to idea.createdAt,
                                JourneyStage.EM_ANALISE to idea.reviewedAt,
                                JourneyStage.APROVADA to idea.reviewedAt,
                                JourneyStage.EM_EXECUCAO to null,
                                JourneyStage.RESULTADO to null
                            ),
                            status = idea.status,
                            rejectionComment = idea.reviewComment
                        )
                        if (idea.status == IdeaStatus.SUBMETIDA) {
                            Spacer(Modifier.height(8.dp))
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                OutlinedButton(onClick = { showDelete = true }, modifier = Modifier.weight(1f)) { Text("Excluir") }
                            }
                        }
                    }

                    if (canCurate) {
                        Spacer(Modifier.height(8.dp))
                        IceMatrix(
                            initial = idea.ice,
                            onSave = { ice -> vm.saveIce(idea.id, ice, session.uid) {} }
                        )
                        Spacer(Modifier.height(8.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(
                                onClick = {
                                    vm.approve(idea.id, session.uid, session.name) { onBack() }
                                },
                                modifier = Modifier.weight(1f),
                                enabled = idea.ice?.isComplete == true
                            ) { Text("Aprovar") }
                            OutlinedButton(onClick = { showReject = true }, modifier = Modifier.weight(1f)) { Text("Rejeitar") }
                        }
                    }
                }
            }
            is UiState.Loading, UiState.Idle -> Text("Carregando…", modifier = Modifier.padding(padding))
            is UiState.Error -> Text("Erro ao carregar ideia.", modifier = Modifier.padding(padding))
        }
    }

    if (showReject) {
        AlertDialog(
            onDismissRequest = { showReject = false },
            title = { Text("Rejeitar ideia") },
            text = {
                OutlinedTextField(
                    value = rejectComment, onValueChange = { rejectComment = it },
                    label = { Text("Motivo (obrigatório)") }, modifier = Modifier.fillMaxWidth()
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        val s = (state as? UiState.Success)?.data?.idea ?: return@TextButton
                        vm.reject(s.id, session.uid, rejectComment) { showReject = false; onBack() }
                    },
                    enabled = rejectComment.isNotBlank()
                ) { Text("Confirmar") }
            },
            dismissButton = { TextButton(onClick = { showReject = false }) { Text("Cancelar") } }
        )
    }

    if (showDelete) {
        AlertDialog(
            onDismissRequest = { showDelete = false },
            title = { Text("Excluir ideia?") },
            text = { Text("Esta ação é permanente. Seus pontos serão revertidos.") },
            confirmButton = {
                TextButton(onClick = {
                    val s = (state as? UiState.Success)?.data?.idea ?: return@TextButton
                    vm.delete(s.id, session.uid) { showDelete = false; onBack() }
                }) { Text("Excluir") }
            },
            dismissButton = { TextButton(onClick = { showDelete = false }) { Text("Cancelar") } }
        )
    }
}
