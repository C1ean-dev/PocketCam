package com.pocketcam.android.updates

data class AppVersion(
    val major: Int,
    val minor: Int,
    val patch: Int,
) : Comparable<AppVersion> {
    override fun compareTo(other: AppVersion): Int =
        compareValuesBy(this, other, AppVersion::major, AppVersion::minor, AppVersion::patch)

    override fun toString(): String = "$major.$minor.$patch"

    companion object {
        private val versionPattern = Regex("^(\\d+)\\.(\\d+)(?:\\.(\\d+))?$")

        fun parse(value: String?): AppVersion? {
            if (value.isNullOrBlank()) return null
            val normalized = value.trim().removePrefix("v").removePrefix("V")
                .substringBefore('-').substringBefore('+')
            val match = versionPattern.matchEntire(normalized) ?: return null
            return AppVersion(
                match.groupValues[1].toIntOrNull() ?: return null,
                match.groupValues[2].toIntOrNull() ?: return null,
                match.groupValues[3].ifEmpty { "0" }.toIntOrNull() ?: return null,
            )
        }
    }
}
