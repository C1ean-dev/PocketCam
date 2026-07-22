package com.pocketcam.android.camera

internal class OrderedResultQueue<T : Any> {
    private val completed = HashMap<Long, T?>()
    private var generation = 0L
    private var nextSequence = 0L

    @Synchronized
    fun reset(generation: Long, nextSequence: Long) {
        this.generation = generation
        this.nextSequence = nextSequence
        completed.clear()
    }

    @Synchronized
    fun complete(generation: Long, sequence: Long, result: T?): List<T> {
        if (generation != this.generation || sequence < nextSequence) return emptyList()
        completed[sequence] = result
        val ready = ArrayList<T>()
        while (completed.containsKey(nextSequence)) {
            completed.remove(nextSequence)?.let(ready::add)
            nextSequence++
        }
        return ready
    }
}
