package com.aguiabranca.app.feature.auth.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.error.toPtBr
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.state.UiState

@Composable
fun LoginScreen(
    onLoggedIn: (Role) -> Unit,
    vm: LoginViewModel = hiltViewModel()
) {
    val form by vm.form.collectAsState()
    val state by vm.state.collectAsState()

    LaunchedEffect(state) {
        val s = state
        if (s is UiState.Success) {
            onLoggedIn(s.data)
        }
    }

    Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.primary) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .windowInsetsPadding(WindowInsets.safeDrawing)
                .padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Image(
                painter = painterResource(id = com.aguiabranca.app.R.drawable.ic_splash_logo),
                contentDescription = "Logotipo Águia Branca",
                modifier = Modifier.size(96.dp)
            )
            Spacer(Modifier.height(8.dp))
            Text("ÁGUIA BRANCA", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 24.sp)
            Text("Inovação que conecta", color = Color.White.copy(alpha = 0.85f), fontSize = 14.sp)
            Spacer(Modifier.height(32.dp))

            Surface(
                color = Color.White,
                shape = androidx.compose.foundation.shape.RoundedCornerShape(20.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(modifier = Modifier.padding(20.dp)) {
                    val keyboard = LocalSoftwareKeyboardController.current
                    val canSubmit = form.email.isNotBlank() && form.password.length >= 6 && state !is UiState.Loading
                    val onSubmit: () -> Unit = {
                        keyboard?.hide()
                        if (canSubmit) vm.submit()
                    }
                    OutlinedTextField(
                        value = form.email,
                        onValueChange = {
                            vm.onEmailChange(it)
                            vm.consumeError()
                        },
                        label = { Text("Email") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        keyboardOptions = KeyboardOptions(
                            keyboardType = KeyboardType.Email,
                            imeAction = ImeAction.Next
                        )
                    )
                    Spacer(Modifier.height(12.dp))
                    OutlinedTextField(
                        value = form.password,
                        onValueChange = {
                            vm.onPasswordChange(it)
                            vm.consumeError()
                        },
                        label = { Text("Senha") },
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(
                            keyboardType = KeyboardType.Password,
                            imeAction = ImeAction.Done
                        ),
                        keyboardActions = KeyboardActions(onDone = { onSubmit() }),
                        modifier = Modifier.fillMaxWidth()
                    )
                    val errorText = (state as? UiState.Error)?.error?.toPtBr()
                    if (errorText != null) {
                        Spacer(Modifier.height(8.dp))
                        Text(errorText, color = MaterialTheme.colorScheme.error, fontSize = 13.sp)
                    }
                    Spacer(Modifier.height(16.dp))
                    Button(
                        onClick = onSubmit,
                        enabled = canSubmit,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        if (state is UiState.Loading) {
                            CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp, color = Color.White)
                        } else {
                            Text("Entrar")
                        }
                    }
                }
            }

            Spacer(Modifier.height(20.dp))
            Text(
                "Entrar como demo",
                color = Color.White.copy(alpha = 0.85f),
                fontSize = 12.sp
            )
            Spacer(Modifier.height(8.dp))
            val quickEnabled = state !is UiState.Loading
            val quickColors = ButtonDefaults.outlinedButtonColors(
                contentColor = Color.White,
                disabledContentColor = Color.White.copy(alpha = 0.4f)
            )
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedButton(
                    onClick = { vm.quickLogin("lider@aguiabranca.com", "aguiabranca123") },
                    enabled = quickEnabled,
                    colors = quickColors,
                    modifier = Modifier.weight(1f)
                ) { Text("Líder") }
                OutlinedButton(
                    onClick = { vm.quickLogin("gestor@aguiabranca.com", "aguiabranca123") },
                    enabled = quickEnabled,
                    colors = quickColors,
                    modifier = Modifier.weight(1f)
                ) { Text("Gestor") }
                OutlinedButton(
                    onClick = { vm.quickLogin("operador@aguiabranca.com", "aguiabranca123") },
                    enabled = quickEnabled,
                    colors = quickColors,
                    modifier = Modifier.weight(1f)
                ) { Text("Operador") }
            }
        }
    }
}
