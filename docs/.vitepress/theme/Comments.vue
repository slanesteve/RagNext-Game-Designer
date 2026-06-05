<template>
  <div ref="container" class="comments-container"></div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'
import { useRoute, useData } from 'vitepress'

const container = ref(null)
const route = useRoute()
const { isDark } = useData()

const loadUtterances = () => {
  if (!container.value) return
  container.value.innerHTML = ''
  
  const script = document.createElement('script')
  script.src = 'https://utteranc.es/client.js'
  script.setAttribute('repo', 'slanesteve/RagNext-Game-Designer')
  script.setAttribute('issue-term', 'pathname')
  script.setAttribute('label', 'documentation-comment')
  script.setAttribute('theme', isDark.value ? 'github-dark' : 'github-light')
  script.setAttribute('crossorigin', 'anonymous')
  script.async = true
  
  container.value.appendChild(script)
}

onMounted(() => {
  loadUtterances()
})

// Reload comments when switching pages
watch(() => route.path, () => {
  loadUtterances()
})

// Update theme when dark/light mode switches
watch(isDark, () => {
  loadUtterances()
})
</script>

<style scoped>
.comments-container {
  margin-top: 40px;
  border-top: 1px solid var(--vp-c-divider);
  padding-top: 20px;
}
</style>
