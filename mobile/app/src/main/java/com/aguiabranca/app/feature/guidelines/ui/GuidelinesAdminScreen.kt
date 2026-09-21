package com.aguiabranca.app.feature.guidelines.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.ui.local.LocalSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GuidelinesAdminScreen(
    guidelineId: String?,
    onDone: () -> Unit,
    vm: GuidelinesAdminViewModel = hiltViewModel()
) {
    LaunchedEffect(guidelineId) { vm.load(guidelineId) }
    val form by vm.form.collectAsState()
    val session = LocalSession.current ?: return
    var pillarExpanded by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text(if (guidelineId == null) "Nova orientação" else "Editar orientação") },
                navigationIcon = { TextButton(onClick = onDone) { Text("Cancelar") } }
            )
        }
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedTextField(
                value = form.title,
                onValueChange = vm::onTitle,
                label = { Text("Título") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            OutlinedTextField(
                value = form.description,
                onValueChange = vm::onDescription,
                label = { Text("Descrição") },
                modifier = Modifier.fillMaxWidth().height(160.dp)
            )
            ExposedDropdownMenuBox(expanded = pillarExpanded, onExpandedChange = { pillarExpanded = it }) {
                OutlinedTextField(
                    value = pillarLabel(form.pillar),
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("Pilar") },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = pillarExpanded) },
                    modifier = Modifier.menuAnchor().fillMaxWidth()
                )
                ExposedDropdownMenu(expanded = pillarExpanded, onDismissRequest = { pillarExpanded = false }) {
                    Pillar.entries.forEach { p ->
                        DropdownMenuItem(text = { Text(pillarLabel(p)) }, onClick = { vm.onPillar(p); pillarExpanded = false })
                    }
                }
            }
            form.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            Spacer(Modifier.height(8.dp))
            Button(
                enabled = form.title.isNotBlank() && !form.saving,
                onClick = { vm.save(session.uid, session.name, onDone) },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(if (form.saving) "Salvando…" else "Salvar")
            }
        }
    }
}

private fun pillarLabel(p: Pillar) = when (p) {
    Pillar.DIRECIONAMENTO -> "Direcionamento"
    Pillar.IDEIAS -> "Ideias"
    Pillar.PROJETOS -> "Projetos"
    Pillar.MENSURACAO -> "Mensuração"
}
