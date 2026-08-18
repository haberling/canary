// Shared behavior for the slideshow widget (see slideshow.html for the
// template/data contract) -- referenced once per page via <script defer>,
// not duplicated per widget instance, unlike the old per-instance inline
// onclick strings this replaced. Autoplay state and the running interval
// are kept as expandos on each widget's root element (r.__timer,
// r.__playing), same idea as before, just real code instead of generated
// strings.
//
// Clicks and keyboard nav use event delegation on `document`, so a widget
// instance added later (hybrid mode's fragment-fetch swap) is handled
// automatically -- no re-init call needed anywhere in the router code. The
// one thing delegation can't cover
// is "run setup once when a new widget instance first appears" (hiding
// nav/dots for a single-slide show, starting autoplay once the first image
// loads) -- a MutationObserver handles that, the standard way to react to
// dynamically-inserted DOM content.

const AUTOPLAY_MS = 3000;

function slideCount(root) {
  return root.querySelectorAll(".slideshow-slide").length;
}

function currentIndex(root) {
  return parseInt(root.dataset.index || "0", 10);
}

function goTo(root, index) {
  const track = root.querySelector(".slideshow-track");
  const n = slideCount(root);
  if (n === 0) return;
  const i = ((index % n) + n) % n;
  root.dataset.index = i;
  track.style.transform = `translateX(-${i * 100}%)`;
  root.querySelectorAll(".slideshow-dot").forEach((dot, j) => dot.classList.toggle("active", j === i));
  const counter = root.querySelector(".slideshow-counter");
  if (counter) counter.textContent = `${i + 1} / ${n}`;
}

function startAutoplay(root) {
  if (root.__timer) return;
  root.__playing = true;
  root.__timer = setInterval(() => {
    if (!root.isConnected) {
      clearInterval(root.__timer);
      return;
    }
    goTo(root, currentIndex(root) + 1);
  }, AUTOPLAY_MS);
}

function stopAutoplay(root) {
  clearInterval(root.__timer);
  root.__timer = null;
  root.__playing = false;
}

function restartAutoplayIfPlaying(root) {
  if (root.__playing) {
    stopAutoplay(root);
    startAutoplay(root);
  }
}

function enhance(root) {
  if (root.__enhanced) return;
  root.__enhanced = true;

  const interactive = slideCount(root) > 1;
  root.querySelectorAll(".slideshow-nav, .slideshow-toggle, .slideshow-footer").forEach((el) => {
    el.style.display = interactive ? "" : "none";
  });
  if (!interactive) return;

  goTo(root, 0);

  const firstImg = root.querySelector(".slideshow-slide img");
  if (!firstImg) return;
  const begin = () => startAutoplay(root);
  if (firstImg.complete) {
    begin();
  } else {
    firstImg.addEventListener("load", begin, { once: true });
    firstImg.addEventListener("error", begin, { once: true });
  }
}

function enhanceAll(root) {
  root.querySelectorAll('[data-widget="slideshow"]').forEach(enhance);
}

enhanceAll(document);
new MutationObserver(() => enhanceAll(document)).observe(document.body, { childList: true, subtree: true });

document.addEventListener("click", (e) => {
  const root = e.target.closest('[data-widget="slideshow"]');
  if (!root) return;

  if (e.target.closest(".slideshow-prev")) {
    goTo(root, currentIndex(root) - 1);
    restartAutoplayIfPlaying(root);
  } else if (e.target.closest(".slideshow-next")) {
    goTo(root, currentIndex(root) + 1);
    restartAutoplayIfPlaying(root);
  } else if (e.target.closest(".slideshow-dot")) {
    const dots = Array.from(root.querySelectorAll(".slideshow-dot"));
    goTo(root, dots.indexOf(e.target.closest(".slideshow-dot")));
    restartAutoplayIfPlaying(root);
  } else if (e.target.closest(".slideshow-toggle")) {
    const button = e.target.closest(".slideshow-toggle");
    if (root.__playing) {
      stopAutoplay(root);
      button.textContent = "▶";
      button.setAttribute("aria-label", "Play slideshow");
    } else {
      startAutoplay(root);
      button.textContent = "⏸";
      button.setAttribute("aria-label", "Pause slideshow");
    }
  }
});

document.addEventListener("keydown", (e) => {
  if (e.key !== "ArrowLeft" && e.key !== "ArrowRight") return;
  const root = document.activeElement && document.activeElement.closest('[data-widget="slideshow"]');
  if (!root) return;
  e.preventDefault();
  goTo(root, currentIndex(root) + (e.key === "ArrowLeft" ? -1 : 1));
  restartAutoplayIfPlaying(root);
});
