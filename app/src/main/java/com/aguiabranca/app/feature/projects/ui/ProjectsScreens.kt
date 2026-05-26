package com.aguiabranca.app.feature.projects.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.components.GuidelineBadge
import com.aguiabranca.app.core.ui.components.GuidelinePicker
import com.aguiabranca.app.core.ui.components.NavTab
import com.aguiabranca.app.core.ui.components.RoleScaffold
import com.aguiabranca.app.core.ui.components.StageBadge
import com.aguiabranca.app.core.ui.components.TimelineEntry
import com.aguiabranca.app.core.ui.components.formatCurrency
import com.aguiabranca.app.core.ui.components.formatDateShort
import com.aguiabranca.app.core.ui.components.formatPercent
import com.aguiabranca.app.core.ui.local.LocalSession
import com.aguiabranca.app.feature.ideas.ui.divisionLabel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProjectsListScreen(
    onOpenProject: (String) -> Unit,
    onNewProject: () -> Unit,
    onTab: (NavTab) -> Unit,
    vm: ProjectsListViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    val items by vm.projects.collectAsState()

    RoleScaffold(
        role = session.role,
        currentTab = NavTab.PROJECTS,
        onTabClick = onTab,
        topBar = { TopAppBar(title = { Text("Projetos") }) }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            if (items.isEmpty()) {
                Column(
                    modifier = Modifier.fillMaxSize().padding(32.dp),
                    verticalArrangement = Arrangement.Center,
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text("Nenhum projeto cadastrado", fontWeight = FontWeight.SemiBold, fontSize = 16.sp)
                    Spacer(Modifier.height(8.dp))
                    Text(
                        "Aprovar uma ideia na curadoria cria automaticamente um projeto em planejamento.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        fontSize = 13.sp,
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center
                    )
                }
            }
            LazyColumn(modifier = Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp)) {
                items(items, key = { it.id }) { p ->
                    Surface(
                        shape = RoundedCornerShape(14.dp),
                        color = MaterialTheme.colorScheme.surface,
                        modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp).clickable { onOpenProject(p.id) }
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(p.title, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                                StageBadge(p.stage)
                            }
                            Spacer(Modifier.height(4.dp))
                            Text("atualizado em ${formatDateShort(p.updatedAt)}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            if (p.guidelineId != null) {
                                Spacer(Modifier.height(6.dp))
                                GuidelineBadge(title = "Vinculado a orientação")
                            }
                        }
                    }
                }
            }
            if (session.role == Role.GESTOR) {
                ExtendedFloatingActionButton(
                    onClick = onNewProject,
                    icon = { Icon(Icons.Outlined.Add, contentDescription = null) },
                    text = { Text("Novo") },
                    modifier = Modifier.align(Alignment.BottomEnd).padding(16.dp)
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProjectDetailScreen(
    projectId: String,
    onBack: () -> Unit,
    onEdit: () -> Unit,
    vm: ProjectDetailViewModel = hiltViewModel()
) {
    LaunchedEffect(projectId) { vm.setProjectId(projectId) }
    val session = LocalSession.current ?: return
    val ui by vm.ui.collectAsState()
    val project = ui.project
    var showDelete by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Projeto") },
                navigationIcon = { TextButton(onClick = onBack) { Text("Voltar") } },
                actions = {
                    if (session.role == Role.GESTOR && project != null) {
                        IconButton(onClick = onEdit) { Icon(Icons.Outlined.Edit, contentDescription = "Editar") }
                        IconButton(onClick = { showDelete = true }) { Icon(Icons.Outlined.Delete, contentDescription = "Excluir") }
                    }
                }
            )
        }
    ) { padding ->
        if (project == null) {
            Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) { Text("Carregando…") }
            return@Scaffold
        }
        Column(modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp).verticalScroll(rememberScrollState())) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(project.title, fontWeight = FontWeight.Bold, fontSize = 20.sp, modifier = Modifier.weight(1f))
                StageBadge(project.stage)
            }
            if (ui.guidelineTitle != null) {
                Spacer(Modifier.height(6.dp))
                GuidelineBadge(title = ui.guidelineTitle)
            }
            Spacer(Modifier.height(8.dp))
            Text(project.description)
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                Kpi("Invest.", formatCurrency(project.investment), Modifier.weight(1f))
                Kpi("Retorno", formatCurrency(project.financialReturn), Modifier.weight(1f))
            }
            Spacer(Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                Kpi("Produtividade", formatPercent(project.productivityGain), Modifier.weight(1f))
                Kpi("Redução custo", formatCurrency(project.costReduction), Modifier.weight(1f))
            }
            Spacer(Modifier.height(8.dp))
            Text("Prazo: ${formatDateShort(project.targetDate)}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 13.sp)
            Spacer(Modifier.height(16.dp))
            Text("Histórico", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(8.dp))
            ui.updates.forEachIndexed { idx, update ->
                TimelineEntry(update = update, isLast = idx == ui.updates.lastIndex)
            }
            Spacer(Modifier.height(24.dp))
        }
    }

    if (showDelete) {
        AlertDialog(
            onDismissRequest = { showDelete = false },
            title = { Text("Excluir projeto?") },
            text = { Text("Esta ação é permanente.") },
            confirmButton = {
                TextButton(onClick = { vm.delete(projectId) { showDelete = false; onBack() } }) { Text("Excluir") }
            },
            dismissButton = { TextButton(onClick = { showDelete = false }) { Text("Cancelar") } }
        )
    }
}

@Composable
private fun Kpi(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(modifier = modifier, shape = RoundedCornerShape(12.dp), color = MaterialTheme.colorScheme.surface) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(label, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(value, fontWeight = FontWeight.Bold, fontSize = 16.sp)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NewProjectScreen(
    onDone: (String) -> Unit,
    onCancel: () -> Unit,
    vm: ProjectFormViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    LaunchedEffect(Unit) { vm.load(null) }
    ProjectFormUi(title = "Novo projeto", isEditing = false, onCancel = onCancel, onDone = onDone, vm = vm, session)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProjectEditScreen(
    projectId: String,
    onDone: () -> Unit,
    onCancel: () -> Unit,
    vm: ProjectFormViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    LaunchedEffect(projectId) { vm.load(projectId) }
    ProjectFormUi(title = "Editar projeto", isEditing = true, onCancel = onCancel, onDone = { onDone() }, vm = vm, session)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ProjectFormUi(
    title: String,
    isEditing: Boolean,
    onCancel: () -> Unit,
    onDone: (String) -> Unit,
    vm: ProjectFormViewModel,
    session: com.aguiabranca.app.core.domain.model.AuthSession
) {
    val form by vm.form.collectAsState()
    val guidelines by vm.guidelines.collectAsState()
    var stageExpanded by remember { mutableStateOf(false) }
    var divExpanded by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text(title) },
                navigationIcon = { TextButton(onClick = onCancel) { Text("Cancelar") } }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp).verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            OutlinedTextField(form.title, vm::onTitle, label = { Text("Título") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(form.description, vm::onDescription, label = { Text("Descrição") }, modifier = Modifier.fillMaxWidth().height(120.dp))
            ExposedDropdownMenuBox(expanded = stageExpanded, onExpandedChange = { stageExpanded = it }) {
                OutlinedTextField(
                    value = stageLabel(form.stage), onValueChange = {}, readOnly = true,
                    label = { Text("Etapa") },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = stageExpanded) },
                    modifier = Modifier.menuAnchor().fillMaxWidth()
                )
                ExposedDropdownMenu(expanded = stageExpanded, onDismissRequest = { stageExpanded = false }) {
                    ProjectStage.entries.forEach { s ->
                        DropdownMenuItem(text = { Text(stageLabel(s)) }, onClick = { vm.onStage(s); stageExpanded = false })
                    }
                }
            }
            OutlinedTextField(form.statusText, vm::onStatusText, label = { Text("Status textual") }, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(form.investment, vm::onInvestment, label = { Text("Investimento (R$)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(form.financialReturn, vm::onFinancialReturn, label = { Text("Retorno financeiro (R$)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(form.productivityGain, vm::onProductivityGain, label = { Text("Ganho de produtividade (%)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(form.costReduction, vm::onCostReduction, label = { Text("Redução de custo (R$)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            ExposedDropdownMenuBox(expanded = divExpanded, onExpandedChange = { divExpanded = it }) {
                OutlinedTextField(
                    value = divisionLabel(form.division), onValueChange = {}, readOnly = true,
                    label = { Text("Divisão") },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = divExpanded) },
                    modifier = Modifier.menuAnchor().fillMaxWidth()
                )
                ExposedDropdownMenu(expanded = divExpanded, onDismissRequest = { divExpanded = false }) {
                    Division.entries.forEach { d ->
                        DropdownMenuItem(text = { Text(divisionLabel(d)) }, onClick = { vm.onDivision(d); divExpanded = false })
                    }
                }
            }
            GuidelinePicker(guidelines = guidelines, selectedId = form.guidelineId, onSelect = vm::onGuideline)
            if (isEditing) {
                OutlinedTextField(form.note, vm::onNote, label = { Text("Nota desta atualização (opcional)") }, modifier = Modifier.fillMaxWidth())
            }
            form.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            Spacer(Modifier.height(8.dp))
            Button(
                enabled = !form.saving && form.title.isNotBlank(),
                onClick = { vm.submit(session.uid, session.name) { id -> onDone(id) } },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(if (form.saving) "Salvando…" else "Salvar")
            }
        }
    }
}

private fun stageLabel(s: ProjectStage) = when (s) {
    ProjectStage.PLANEJAMENTO -> "Planejamento"
    ProjectStage.EM_EXECUCAO -> "Em execução"
    ProjectStage.CONCLUIDO -> "Concluído"
    ProjectStage.CANCELADO -> "Cancelado"
}
