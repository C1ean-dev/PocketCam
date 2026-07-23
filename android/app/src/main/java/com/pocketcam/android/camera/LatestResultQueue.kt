package com.pocketcam.android.camera

/**
 * Releases the newest completed result immediately instead of waiting for an
 * older encode task. Live video prefers a fresh frame over strict sequence
 * ordering: waiting for a slow JPEG creates visible queueing delay.
 */
internal class LatestResultQueue<T : Any> {
    private var generation = 0L
    private var newestPublishedSequence = -1L

    @Synchronized
    fun reset(generation: Long, nextSequence: Long) {
        this.generation = generation
        newestPublishedSequence = nextSequence - 1
    }

    @Synchronized
    fun complete(generation: Long, sequence: Long, result: T?): List<T> {
        if (generation != this.generation || sequence <= newestPublishedSequence) return emptyList()
        newestPublishedSequence = sequence
        return result?.let(::listOf) ?: emptyList()
    }
}
