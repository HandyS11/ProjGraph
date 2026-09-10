/*
 * ProjGraph documentation theme behaviour.
 *
 * Uses only docfx's documented `main.js` extension points (`defaultTheme`,
 * `iconLinks`, `mermaid`, `start`) so a docfx upgrade does not break it.
 */

const COPY_SHORTCUT = /Mac|iPhone|iPad/.test(navigator.userAgent)
  ? '⌘C'
  : 'Ctrl+C'

/** Puts a node's text under the user's selection, ready for a manual copy. */
function selectContents(node) {
  const range = document.createRange()
  range.selectNodeContents(node)
  const selection = window.getSelection()
  selection.removeAllRanges()
  selection.addRange(range)
}

/** Copies the adjacent command, then reports the result on the button itself. */
function wireCopyButtons() {
  for (const button of document.querySelectorAll('.pg-copy')) {
    const code = button.parentElement?.querySelector('code')
    if (!code) {
      continue
    }

    const label = button.textContent
    let revertTimer

    /* Every outcome reverts to the original label, so the button can never be
       left showing a stale message. */
    const report = (message, copied, revertAfterMs) => {
      clearTimeout(revertTimer)
      button.textContent = message
      button.dataset.copied = copied
      revertTimer = setTimeout(() => {
        button.textContent = label
        button.dataset.copied = 'false'
      }, revertAfterMs)
    }

    button.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(code.textContent.trim())
        report('Copied', 'true', 1600)
      } catch {
        /* Clipboard access can be refused - over plain HTTP, or by permission.
           Select the command first so the shortcut has something to act on. */
        selectContents(code)
        report(`Press ${COPY_SHORTCUT}`, 'false', 4000)
      }
    })
  }
}

/*
 * The landing page's only non-user-triggered motion: the hero graph draws its
 * own edges once, then the cardinality markers arrive. Each edge is measured so
 * the dash animation matches its real length instead of a guessed constant.
 */
function drawHeroGraph() {
  const transform = document.querySelector('.pg-transform')
  if (!transform || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    return
  }

  const edges = transform.querySelectorAll('.pg-g-edge')
  if (edges.length === 0) {
    return
  }

  for (const edge of edges) {
    /* getTotalLength() throws on a detached or zero-length node; an edge we
       cannot measure simply keeps its static state. */
    try {
      edge.style.setProperty('--pg-len', Math.ceil(edge.getTotalLength()))
    } catch {
      continue
    }
  }

  transform.dataset.draw = 'pending'
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      transform.dataset.draw = 'running'
    })
  })
}

function enhance() {
  wireCopyButtons()
  drawHeroGraph()
}

export default {
  defaultTheme: 'dark',

  /* docfx merges this over its own `{ startOnLoad, theme }`, picking the base
     theme from the current light/dark setting. Only theme-agnostic values are
     set here: anything with a fixed lightness would be wrong in one of the two
     themes, so surfaces and text are left to mermaid's own ramp. */
  mermaid: {
    fontFamily:
      "'ProjGraph Mono', ui-monospace, 'SFMono-Regular', Consolas, monospace",
    themeVariables: {
      lineColor: '#22a5c4',
    },
  },

  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/HandyS11/ProjGraph',
      title: 'Source on GitHub',
    },
    {
      icon: 'box-seam',
      href: 'https://www.nuget.org/packages/ProjGraph.Cli',
      title: 'Package on NuGet',
    },
  ],

  start: () => {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', enhance, { once: true })
    } else {
      enhance()
    }
  },
}
