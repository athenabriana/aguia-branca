package com.aguiabranca.app.feature.ideas.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.ui.components.CategoryAutoCompleteField
import com.aguiabranca.app.core.ui.components.GuidelinePicker
import com.aguiabranca.app.core.ui.local.LocalSession
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NewIdeaScreen(
    onDone: () -> Unit,
    onCancel: () -> Unit,
    vm: NewIdeaViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    LaunchedEffect(Unit) { vm.init(session.division) }
    val form by vm.form.collectAsState()
    val guidelines by vm.guidelines.collectAsState()
    var divExpanded by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Nova ideia") },
                navigationIcon = { TextButton(onClick = onCancel) { Text("Cancelar") } }
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) { Snackbar { Text(it.visuals.message) } } }
    ) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp).verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            OutlinedTextField(value = form.title, onValueChange = vm::onTitle, label = { Text("Título") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(value = form.description, onValueChange = vm::onDesc, label = { Text("Descrição") }, modifier = Modifier.fillMaxWidth().height(140.dp))
            CategoryAutoCompleteField(value = form.category, onValueChange = vm::onCategory, suggestionsLoader = { vm.searchCategoryPrefix(it) })
            ExposedDropdownMenuBox(expanded = divExpanded, onExpandedChange = { divExpanded = it }) {
                OutlinedTextField(
                    value = divisionLabel(form.division),
                    onValueChange = {},
                    readOnly = true,
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
            GuidelinePicker(
                guidelines = guidelines,
                selectedId = form.guidelineId,
                onSelect = vm::onGuideline
            )
            form.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            Spacer(Modifier.height(8.dp))
            Button(
                onClick = {
                    vm.submit(session.uid, session.name) { toast ->
                        scope.launch { snackbarHostState.showSnackbar(toast) }
                        onDone()
                    }
                },
                enabled = !form.saving,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(if (form.saving) "Enviando…" else "Enviar ideia")
            }
        }
    }
}

internal fun divisionLabel(d: Division) = when (d) {
    Division.PASSAGEIROS -> "Passageiros"
    Division.COMERCIO -> "Comércio"
    Division.LOGISTICA -> "Logística"
    Division.CORPORATIVO -> "Corporativo"
}
