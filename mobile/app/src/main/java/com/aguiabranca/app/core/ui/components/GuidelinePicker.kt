package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.aguiabranca.app.core.domain.model.Guideline

@Composable
fun GuidelinePicker(
    guidelines: List<Guideline>,
    selectedId: String?,
    onSelect: (String?) -> Unit,
    modifier: Modifier = Modifier
) {
    val selected = guidelines.find { it.id == selectedId }
    Column(modifier = modifier.fillMaxWidth()) {
        if (guidelines.isEmpty()) {
            OutlinedTextField(
                value = "Nenhuma orientação cadastrada ainda",
                onValueChange = {},
                readOnly = true,
                enabled = false,
                label = { Text("Orientação estratégica") },
                modifier = Modifier.fillMaxWidth()
            )
        } else {
            var expanded by remember { mutableStateOf(false) }
            ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
                OutlinedTextField(
                    value = selected?.title ?: "(sem vínculo)",
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("Orientação estratégica") },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
                    modifier = Modifier.menuAnchor().fillMaxWidth()
                )
                ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                    DropdownMenuItem(
                        text = { Text("Sem vínculo") },
                        onClick = { onSelect(null); expanded = false }
                    )
                    guidelines.forEach { g ->
                        DropdownMenuItem(
                            text = { Text(g.title) },
                            onClick = { onSelect(g.id); expanded = false }
                        )
                    }
                }
            }
            if (selected != null) {
                Spacer(Modifier.height(8.dp))
                GuidelineBadge(title = selected.title)
            }
        }
    }
}
