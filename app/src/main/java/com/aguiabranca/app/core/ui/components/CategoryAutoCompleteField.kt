package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier

@Composable
fun CategoryAutoCompleteField(
    value: String,
    onValueChange: (String) -> Unit,
    suggestionsLoader: suspend (String) -> List<String>,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    var suggestions by remember { mutableStateOf<List<String>>(emptyList()) }

    LaunchedEffect(value) {
        if (value.length >= 2) {
            suggestions = try {
                suggestionsLoader(value).filter { it.isNotBlank() }.distinct().take(8)
            } catch (_: Throwable) { emptyList() }
            expanded = suggestions.isNotEmpty()
        } else {
            suggestions = emptyList()
            expanded = false
        }
    }

    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }, modifier = modifier) {
        OutlinedTextField(
            value = value,
            onValueChange = { onValueChange(it.take(40)) },
            label = { Text("Categoria") },
            singleLine = true,
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth()
        )
        if (suggestions.isNotEmpty()) {
            ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                suggestions.forEach { s ->
                    DropdownMenuItem(text = { Text(s) }, onClick = { onValueChange(s); expanded = false })
                }
            }
        }
    }
}
