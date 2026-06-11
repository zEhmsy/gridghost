/*
 * Copyright 2026 sitecVendor. All Rights Reserved.
 */

plugins {
  id("com.tridium.niagara")
  id("com.tridium.vendor")
  id("com.tridium.niagara-signing")
  id("com.tridium.convention.niagara-home-repositories")
}

vendor {
  defaultVendor("sitecVendor")
  defaultModuleVersion("1.0")
}

subprojects {
  repositories {
    mavenCentral()
  }

  if (tasks.findByName("prepareKotlinBuildScriptModel") == null) {
    tasks.register("prepareKotlinBuildScriptModel") {
      group = "ide"
      description = "No-op task required by IDE Gradle import for Kotlin build script model."
    }
  }
}
