import DefaultTheme from 'vitepress/theme'
import { h, onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vitepress'
import mediumZoom from 'medium-zoom'
import Comments from './Comments.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'doc-after': () => h(Comments)
    })
  },
  setup() {
    const route = useRoute()
    const initZoom = () => {
      // Initialize mediumZoom on all images inside the content markdown block (.vp-doc)
      mediumZoom('.vp-doc img', {
        background: 'rgba(0, 0, 0, 0.85)'
      })
    }
    onMounted(() => {
      initZoom()
    })
    watch(
      () => route.path,
      () => nextTick(() => initZoom())
    )
  }
}

