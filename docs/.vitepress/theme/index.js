import DefaultTheme from 'vitepress/theme'
import { h, onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vitepress'
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
      // Find all images inside VitePress document markdown content
      const images = document.querySelectorAll('.vp-doc img')
      images.forEach(img => {
        // Prevent duplicate listener attachments
        if (img.dataset.zoomAttached) return
        img.dataset.zoomAttached = 'true'
        
        img.addEventListener('click', () => {
          // Create modal overlay
          const overlay = document.createElement('div')
          overlay.className = 'custom-zoom-overlay'
          
          // Create zoomed image
          const zoomedImg = document.createElement('img')
          zoomedImg.src = img.src
          zoomedImg.className = 'custom-zoom-image'
          
          overlay.appendChild(zoomedImg)
          document.body.appendChild(overlay)
          
          // Close modal on click
          overlay.addEventListener('click', () => {
            overlay.remove()
          })
          
          // Close modal on ESC key
          const escHandler = (e) => {
            if (e.key === 'Escape') {
              overlay.remove()
              document.removeEventListener('keydown', escHandler)
            }
          }
          document.addEventListener('keydown', escHandler)
        })
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
