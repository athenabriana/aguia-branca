package com.aguiabranca.app.core.network

import android.util.Base64
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.first
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

data class Tokens(val accessToken: String, val refreshToken: String)

/** Guarda a sessão. `clear()` no logout. */
interface TokenStore {
    suspend fun get(): Tokens?
    suspend fun save(tokens: Tokens)
    suspend fun clear()
}

/** Cifra/decifra os tokens antes de irem para o disco. */
interface TokenCipher {
    fun encrypt(plain: String): String
    /** `null` se o valor não puder ser decifrado (chave perdida/dado corrompido): a sessão é tratada como inexistente. */
    fun decrypt(encrypted: String): String?
}

/** AES-256-GCM com chave no Android Keystore (não exportável). Formato: base64(IV[12] + texto cifrado + tag). */
class AndroidKeystoreCipher(private val alias: String = "aguiabranca_tokens_v1") : TokenCipher {

    private fun key(): SecretKey {
        val ks = KeyStore.getInstance(ANDROID_KEYSTORE).apply { load(null) }
        (ks.getKey(alias, null) as? SecretKey)?.let { return it }
        val generator = KeyGenerator.getInstance("AES", ANDROID_KEYSTORE)
        generator.init(
            android.security.keystore.KeyGenParameterSpec.Builder(
                alias, android.security.keystore.KeyProperties.PURPOSE_ENCRYPT or android.security.keystore.KeyProperties.PURPOSE_DECRYPT
            )
                .setBlockModes(android.security.keystore.KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(android.security.keystore.KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build()
        )
        return generator.generateKey()
    }

    override fun encrypt(plain: String): String {
        val cipher = Cipher.getInstance(TRANSFORMATION).apply { init(Cipher.ENCRYPT_MODE, key()) }
        val out = cipher.iv + cipher.doFinal(plain.toByteArray(Charsets.UTF_8))
        return Base64.encodeToString(out, Base64.NO_WRAP)
    }

    override fun decrypt(encrypted: String): String? = runCatching {
        val bytes = Base64.decode(encrypted, Base64.NO_WRAP)
        val cipher = Cipher.getInstance(TRANSFORMATION)
            .apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, bytes.copyOfRange(0, IV_SIZE))) }
        String(cipher.doFinal(bytes, IV_SIZE, bytes.size - IV_SIZE), Charsets.UTF_8)
    }.getOrNull()

    private companion object {
        const val ANDROID_KEYSTORE = "AndroidKeyStore"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
        const val IV_SIZE = 12
    }
}

/** Tokens no DataStore, sempre cifrados (nunca em texto puro nas preferências). */
class DataStoreTokenStore(
    private val dataStore: DataStore<Preferences>,
    private val cipher: TokenCipher
) : TokenStore {

    override suspend fun get(): Tokens? {
        val prefs = dataStore.data.first()
        val access = prefs[ACCESS]?.let(cipher::decrypt) ?: return null
        val refresh = prefs[REFRESH]?.let(cipher::decrypt) ?: return null
        return Tokens(access, refresh)
    }

    override suspend fun save(tokens: Tokens) {
        dataStore.edit {
            it[ACCESS] = cipher.encrypt(tokens.accessToken)
            it[REFRESH] = cipher.encrypt(tokens.refreshToken)
        }
    }

    override suspend fun clear() {
        dataStore.edit { it.remove(ACCESS); it.remove(REFRESH) }
    }

    private companion object {
        val ACCESS = stringPreferencesKey("access_token_enc")
        val REFRESH = stringPreferencesKey("refresh_token_enc")
    }
}
