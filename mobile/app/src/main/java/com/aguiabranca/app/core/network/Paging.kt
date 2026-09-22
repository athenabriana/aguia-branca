package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.network.dto.PagedDto

/** Junta todas as páginas (o app trabalha com listas completas; a API entrega até 200 por página). */
suspend fun <T> fetchAll(fetchPage: suspend (page: Int) -> PagedDto<T>): List<T> {
    val all = mutableListOf<T>()
    var page = 1
    while (true) {
        val result = fetchPage(page)
        all += result.items
        if (result.items.isEmpty() || !result.hasMore) return all
        page++
    }
}
