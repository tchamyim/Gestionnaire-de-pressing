(() => {
    const panel = document.querySelector("[data-naya-presentation]");
    if (!panel) return;

    const caption = panel.querySelector("[data-naya-caption]");
    const state = panel.querySelector("[data-naya-state]");
    const progress = panel.querySelector("[data-naya-progress]");
    const progressBar = panel.querySelector('[role="progressbar"]');
    const startButton = panel.querySelector("[data-naya-start]");
    const stopButton = panel.querySelector("[data-naya-stop]");
    const languageButtons = Array.from(panel.querySelectorAll("[data-naya-language]"));
    const languageLabel = panel.querySelector("[data-naya-language-label]");
    const languageGroup = panel.querySelector("[data-naya-language-group]");
    const kicker = panel.querySelector("[data-naya-kicker]");
    const title = panel.querySelector("[data-naya-title]");
    const canSpeak = "speechSynthesis" in window && "SpeechSynthesisUtterance" in window;

    const copy = {
        fr: {
            ready: "Prête",
            kicker: "IA NAYA · GUIDE VOCAL",
            title: "Elle présente l’application pour vous.",
            languageLabel: "Langue de Naya",
            instruction: "Appuyez une fois pour lancer la visite commentée de Fresh & Care.",
            start: "▶ Démarrer la présentation",
            running: "Présentation en cours…",
            replay: "↻ Recommencer la présentation",
            resume: "▶ Reprendre la présentation",
            stop: "Arrêter",
            finished: "Présentation terminée",
            stopped: "Présentation arrêtée",
            unavailable: "La voix n’est pas disponible sur cet appareil.",
            failed: "La présentation reste lisible à l’écran, mais la synthèse vocale de cet appareil n’est pas disponible.",
            step: (current, total) => `Étape ${current} sur ${total}`,
            stoppedMessage: "Vous pouvez reprendre depuis le début quand vous le souhaitez.",
            presentation: [
                "Bonjour, grand professeur. Je vais vous présenter Fresh & Care, mon application de gestion pour un pressing.",
                "Sur cet accueil, le gérant accède à son espace de travail et chaque client peut ouvrir son espace personnel pour suivre ses vêtements.",
                "Le tableau de bord rassemble les informations importantes : les clients, les tickets en cours, les articles prêts, les impayés et le solde de caisse.",
                "Pour chaque dépôt, le gérant enregistre les vêtements et leur tarif. L’atelier met ensuite à jour le traitement de chaque article jusqu’à son retrait.",
                "La messagerie permet de répondre aux clients. Le pressing peut aussi préparer des notifications et suivre les consommables ainsi que sa grille tarifaire.",
                "Les rapports présentent les encaissements et la clôture de caisse. Dans l’espace client, chacun retrouve ses tickets, ses articles prêts, ses paiements et son solde.",
                "Naya accueille le client par son prénom, répond aux questions de suivi et peut prononcer une phrase. La voix est générée directement sur cet appareil.",
                "Fresh & Care accompagne le linge de son dépôt jusqu’à sa remise, avec un suivi clair et un accueil attentionné.",
                "Merci, grand professeur, pour votre attention !"
            ]
        },
        en: {
            ready: "Ready",
            kicker: "NAYA AI · VOICE GUIDE",
            title: "She presents the application for you.",
            languageLabel: "Naya’s language",
            instruction: "Press once to hear the guided presentation of Fresh & Care.",
            start: "▶ Start presentation",
            running: "Presentation in progress…",
            replay: "↻ Replay presentation",
            resume: "▶ Start again",
            stop: "Stop",
            finished: "Presentation complete",
            stopped: "Presentation stopped",
            unavailable: "Voice playback is unavailable on this device.",
            failed: "The presentation remains visible, but this device’s speech service is unavailable.",
            step: (current, total) => `Step ${current} of ${total}`,
            stoppedMessage: "You can restart the presentation whenever you like.",
            presentation: [
                "Good day, Professor. I’m going to present Fresh & Care, my laundry and dry-cleaning management application.",
                "On this welcome screen, managers open their workspace, while customers enter their personal space to follow their garments.",
                "The dashboard brings key information together: customers, active tickets, ready items, unpaid balances and cash on hand.",
                "For each drop-off, the manager records the garments and their prices. The workshop updates each item’s progress until collection.",
                "The inbox keeps customer conversations organized. Notifications can be prepared, and stock levels and prices can be maintained.",
                "Reports summarize payments and the daily cash closing. In the customer portal, each person can find tickets, ready items, payments and their balance.",
                "Naya welcomes each customer by name, answers tracking questions and can read a phrase aloud. Her voice is generated on this device.",
                "Fresh & Care follows each garment from drop-off to collection with clear tracking and considerate service.",
                "Thank you, Professor, for your attention!"
            ]
        }
    };

    let language = "fr";
    let runId = 0;
    let currentStep = 0;
    try {
        language = window.localStorage.getItem("freshcare.naya.language") === "en" ? "en" : "fr";
    } catch { }

    function selectedCopy() {
        return copy[language];
    }

    function setProgress(value) {
        progress.style.width = `${value}%`;
        progressBar.setAttribute("aria-valuenow", String(value));
    }

    function updateLanguage() {
        const words = selectedCopy();
        languageButtons.forEach(button => button.setAttribute("aria-pressed", String(button.dataset.nayaLanguage === language)));
        languageLabel.textContent = words.languageLabel;
        languageGroup.setAttribute("aria-label", words.languageLabel);
        kicker.textContent = words.kicker;
        title.textContent = words.title;
        state.textContent = words.ready;
        caption.textContent = words.instruction;
        startButton.textContent = words.start;
        stopButton.textContent = words.stop;
    }

    function finish() {
        panel.classList.remove("is-speaking");
        state.textContent = selectedCopy().finished;
        startButton.textContent = selectedCopy().replay;
        startButton.disabled = false;
        stopButton.disabled = true;
        setProgress(100);
    }

    function speakStep(id) {
        if (id !== runId) return;
        const lines = selectedCopy().presentation;
        if (currentStep >= lines.length) {
            finish();
            return;
        }

        const stepIndex = currentStep;
        const text = lines[stepIndex];
        currentStep += 1;
        caption.textContent = text;
        state.textContent = selectedCopy().step(stepIndex + 1, lines.length);
        setProgress(Math.round(stepIndex / lines.length * 100));

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = language === "fr" ? "fr-FR" : "en-US";
        utterance.rate = 0.94;
        utterance.pitch = 1.02;
        const prefix = language === "fr" ? "fr" : "en";
        const preferredVoice = window.speechSynthesis.getVoices().find(voice => voice.lang?.toLowerCase().startsWith(prefix));
        if (preferredVoice) utterance.voice = preferredVoice;
        utterance.onend = () => speakStep(id);
        utterance.onerror = event => {
            if (id !== runId || event.error === "canceled" || event.error === "interrupted") return;
            panel.classList.remove("is-speaking");
            state.textContent = selectedCopy().unavailable;
            caption.textContent = selectedCopy().failed;
            startButton.textContent = selectedCopy().resume;
            startButton.disabled = false;
            stopButton.disabled = true;
        };
        window.speechSynthesis.speak(utterance);
    }

    function startPresentation() {
        if (!canSpeak) {
            caption.textContent = selectedCopy().failed;
            state.textContent = selectedCopy().unavailable;
            return;
        }

        runId += 1;
        currentStep = 0;
        window.speechSynthesis.cancel();
        panel.classList.add("is-speaking");
        state.textContent = selectedCopy().running;
        startButton.textContent = selectedCopy().running;
        startButton.disabled = true;
        stopButton.textContent = selectedCopy().stop;
        stopButton.disabled = false;
        setProgress(0);
        speakStep(runId);
    }

    function changeLanguage(nextLanguage) {
        if (!copy[nextLanguage] || nextLanguage === language) return;
        const wasSpeaking = panel.classList.contains("is-speaking");
        runId += 1;
        if (canSpeak) window.speechSynthesis.cancel();
        panel.classList.remove("is-speaking");
        language = nextLanguage;
        try { window.localStorage.setItem("freshcare.naya.language", language); } catch { }
        updateLanguage();
        setProgress(0);
        if (wasSpeaking) startPresentation();
    }

    languageButtons.forEach(button => button.addEventListener("click", () => changeLanguage(button.dataset.nayaLanguage)));
    startButton.addEventListener("click", startPresentation);
    stopButton.addEventListener("click", () => {
        runId += 1;
        if (canSpeak) window.speechSynthesis.cancel();
        panel.classList.remove("is-speaking");
        state.textContent = selectedCopy().stopped;
        caption.textContent = selectedCopy().stoppedMessage;
        startButton.textContent = selectedCopy().resume;
        startButton.disabled = false;
        stopButton.disabled = true;
        setProgress(0);
    });

    updateLanguage();
})();
