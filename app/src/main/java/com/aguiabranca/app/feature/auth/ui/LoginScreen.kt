package com.aguiabranca.app.feature.auth.ui

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.error.toPtBr
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.state.UiState

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun LoginScreen(
    onLoggedIn: (Role) -> Unit,
    vm: LoginViewModel = hiltViewModel()
) {
    val form by vm.form.collectAsState()
    val state by vm.state.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(state) {
        val s = state
        if (s is UiState.Success) {
            onLoggedIn(s.data)
        }
    }

    LaunchedEffect(form.seedMessage) {
        form.seedMessage?.let {
            snackbarHostState.showSnackbar(it)
            vm.clearSeedMessage()
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
            Box(
                modifier = Modifier
                    .size(96.dp)
                    .clip(androidx.compose.foundation.shape.CircleShape)
                    .combinedClickable(
                        onClick = {},
                        onLongClick = { vm.runSeed() }
                    ),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "IG",
                    color = Color.White,
                    fontSize = 36.sp,
                    fontWeight = FontWeight.Black
                )
            }
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
                    OutlinedTextField(
                        value = form.email,
                        onValueChange = vm::onEmailChange,
                        label = { Text("Email") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email)
                    )
                    Spacer(Modifier.height(12.dp))
                    OutlinedTextField(
                        value = form.password,
                        onValueChange = vm::onPasswordChange,
                        label = { Text("Senha") },
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                        modifier = Modifier.fillMaxWidth()
                    )
                    val errorText = (state as? UiState.Error)?.error?.toPtBr()
                    if (errorText != null) {
                        Spacer(Modifier.height(8.dp))
                        Text(errorText, color = MaterialTheme.colorScheme.error, fontSize = 13.sp)
                    }
                    Spacer(Modifier.height(16.dp))
                    val canSubmit = form.email.isNotBlank() && form.password.length >= 6 && state !is UiState.Loading
                    Button(
                        onClick = { vm.submit() },
                        enabled = canSubmit,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        if (state is UiState.Loading) {
                            CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp, color = Color.White)
                        } else {
                            Text("Entrar")
                        }
                    }
                    Spacer(Modifier.height(8.dp))
                    Text(
                        "Pressione o logo para popular credenciais demo",
                        fontSize = 11.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    if (form.seedRunning) {
                        Spacer(Modifier.height(8.dp))
                        Text("Gerando dados de demonstração…", fontSize = 12.sp)
                    }
                }
            }
            Spacer(Modifier.height(24.dp))
            SnackbarHost(hostState = snackbarHostState) { data ->
                Snackbar { Text(data.visuals.message) }
            }
        }
    }
}
