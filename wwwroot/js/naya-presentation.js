(() => {
    const panel = document.querySelector("[data-naya-presentation]");
    if (!panel) return;

    const caption = panel.querySelector("[data-naya-caption]");
    const state = panel.querySelector("[data-naya-state]");
    const progress = panel.querySelector("[data-naya-progress]");
    const progressBar = panel.querySelector('[role="progressbar"]');
    const startButton = panel.querySelector("[data-naya-start]");
    const stopButton = panel.querySelector("[data-naya-stop]");
    const canSpeak = "speechSynthesis" in window && "SpeechSynthesisUtterance" in window;

    const presentation = [
        "Bonjour, grand professeur. Je vais vous présenter Fresh & Care, mon application de gestion pour un pressing.",
        "Sur cet accueil, le gérant accède à son espace de travail et chaque client peut ouvrir son espace personnel pour suivre ses vêtements.",
        "Le tableau de bord rassemble les informations importantes : les clients, les tickets en cours, les articles prêts, les impayés et le solde de caisse.",
        "Pour chaque dépôt, le gérant enregistre les vêtements et leur tarif. L’atelier met ensuite à jour le traitement de chaque article jusqu’à son retrait.",
        "La messagerie permet de répondre aux clients. Le pressing peut aussi préparer des notifications et suivre les consommables ainsi que sa grille tarifaire.",
        "Les rapports présentent les encaissements et la clôture de caisse. Dans l’espace client, chacun retrouve ses tickets, ses articles prêts, ses paiements et son solde.",
        "Naya accueille le client par son prénom, répond aux questions de suivi et peut prononcer une phrase. La voix est générée directement sur cet appareil.",
        "Fresh & Care accompagne le linge de son dépôt jusqu’à sa remise, avec un suivi clair et un accueil attentionné.",
        "Merci, grand professeur, pour votre attention !"
    ];

    let runId = 0;
    let currentStep = 0;

    function setProgress(value) {
        progress.style.width = `${value}%`;
        progressBar.setAttribute("aria-valuenow", String(value));
    }

    function finish() {
        panel.classList.remove("is-speaking");
        state.textContent = "Présentation terminée";
        startButton.textContent = "↻ Recommencer la présentation";
        startButton.disabled = false;
        stopButton.disabled = true;
        setProgress(100);
    }

    function speakStep(id) {
        if (id !== runId) return;
        if (currentStep >= presentation.length) {
            finish();
            return;
        }

        const stepIndex = currentStep;
        const text = presentation[stepIndex];
        currentStep += 1;
        caption.textContent = text;
        state.textContent = `Étape ${stepIndex + 1} sur ${presentation.length}`;
        setProgress(Math.round(stepIndex / presentation.length * 100));

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = "fr-FR";
        utterance.rate = 0.94;
        utterance.pitch = 1.02;
        const frenchVoice = window.speechSynthesis.getVoices().find(voice => voice.lang?.toLowerCase().startsWith("fr"));
        if (frenchVoice) utterance.voice = frenchVoice;
        utterance.onend = () => speakStep(id);
        utterance.onerror = event => {
            if (id !== runId || event.error === "canceled" || event.error === "interrupted") return;
            panel.classList.remove("is-speaking");
            state.textContent = "Voix indisponible";
            caption.textContent = "La présentation reste lisible à l’écran, mais la synthèse vocale de cet appareil n’est pas disponible.";
            startButton.disabled = false;
            stopButton.disabled = true;
        };
        window.speechSynthesis.speak(utterance);
    }

    startButton.addEventListener("click", () => {
        if (!canSpeak) {
            caption.textContent = "La voix n’est pas disponible sur cet appareil. La présentation reste consultable à l’écran.";
            state.textContent = "Lecture vocale indisponible";
            return;
        }

        runId += 1;
        currentStep = 0;
        window.speechSynthesis.cancel();
        panel.classList.add("is-speaking");
        state.textContent = "Présentation en cours";
        startButton.disabled = true;
        stopButton.disabled = false;
        setProgress(0);
        speakStep(runId);
    });

    stopButton.addEventListener("click", () => {
        runId += 1;
        if (canSpeak) window.speechSynthesis.cancel();
        panel.classList.remove("is-speaking");
        state.textContent = "Présentation arrêtée";
        caption.textContent = "Vous pouvez reprendre depuis le début quand vous le souhaitez.";
        startButton.textContent = "▶ Reprendre la présentation";
        startButton.disabled = false;
        stopButton.disabled = true;
    });
})();
