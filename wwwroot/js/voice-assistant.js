(() => {
    const panel = document.querySelector("[data-voice-assistant]");
    if (!panel) return;

    const response = panel.querySelector("[data-voice-response]");
    const firstName = panel.dataset.firstName || "";
    const tickets = Array.from(document.querySelectorAll("[data-voice-ticket]"));
    const speechAvailable = "speechSynthesis" in window && "SpeechSynthesisUtterance" in window;
    const greeting = firstName
        ? `Bonjour ${firstName}, bienvenue chez Fresh & Care. Je suis Naya, votre hôtesse vocale. Je peux vous renseigner sur vos tickets, vos articles prêts et votre solde. Que puis-je faire pour vous ?`
        : "Bonjour et bienvenue chez Fresh & Care. Je suis Naya, votre hôtesse vocale. Comment puis-je vous aider ?";

    function speak(text) {
        if (!speechAvailable) {
            response.textContent = `${text} La synthèse vocale n’est pas disponible sur cet appareil, mais vous pouvez lire ma réponse ici.`;
            return;
        }

        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = "fr-FR";
        utterance.rate = 0.96;
        utterance.pitch = 1.03;
        const frenchVoice = window.speechSynthesis.getVoices().find(voice => voice.lang?.toLowerCase().startsWith("fr"));
        if (frenchVoice) utterance.voice = frenchVoice;
        utterance.onerror = () => {
            response.textContent = `${text} La voix de cet appareil est momentanément indisponible.`;
        };
        window.speechSynthesis.speak(utterance);
    }

    function normalize(value) {
        return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
    }

    function compact(value) {
        return normalize(value).replace(/[^a-z0-9]/g, "");
    }

    function answerQuestion(question) {
        const normalized = normalize(question);
        const compactQuestion = compact(question);
        const namedTicket = tickets.find(row => compactQuestion.includes(compact(row.dataset.voiceTicket || "")));
        const ticketCount = Number(panel.dataset.ticketCount || 0);
        const readyCount = Number(panel.dataset.readyCount || 0);
        const activeCount = Number(panel.dataset.activeCount || 0);
        const name = firstName ? ` ${firstName}` : "";

        if (namedTicket) {
            const ticket = namedTicket.dataset.voiceTicket;
            const status = namedTicket.dataset.voiceStatus || "enregistré";
            const ready = Number(namedTicket.dataset.voiceReadyItems || 0);
            const remaining = namedTicket.dataset.voiceRemaining || "";
            const pickup = ready > 0
                ? `${ready} article${ready > 1 ? "s" : ""} ${ready > 1 ? "sont prêts" : "est prêt"}.`
                : "Aucun article de ce ticket n’est indiqué comme prêt pour le moment.";
            const payment = remaining && !/^0/.test(remaining)
                ? ` Le reste à payer est de ${remaining}.`
                : " Aucun solde ne reste à payer sur ce ticket.";
            return `Bien sûr${name}. Le ticket ${ticket} est au statut ${status}. ${pickup}${payment}`;
        }

        if (/\b(bonjour|bonsoir|salut|coucou|hello)\b/.test(normalized)) {
            return `Bonjour${name} ! Je suis heureuse de vous accueillir chez Fresh & Care. Vos informations de suivi sont à jour dans cet espace.`;
        }

        if (/\b(aide|aider|que peux tu|comment faire|comment utiliser)\b/.test(normalized)) {
            return `Avec plaisir${name}. Demandez-moi si un article est prêt, le suivi d’un ticket ou le montant de votre solde. Pour une question particulière, utilisez le bouton Parler au pressing.`;
        }

        if (/\b(pr[eê]t|pr[eê]te|disponible|recuperer|retrait)\b/.test(normalized)) {
            return readyCount > 0
                ? `Bonne nouvelle${name} : ${readyCount} article${readyCount > 1 ? "s sont" : " est"} prêt${readyCount > 1 ? "s" : ""}. Vous pouvez ouvrir le détail d’un ticket pour voir les articles concernés.`
                : `Pour le moment${name}, aucun article n’est indiqué comme prêt. Consultez votre espace plus tard ou écrivez au pressing pour toute précision.`;
        }

        if (/\b(solde|payer|paiement|reste a payer|montant)\b/.test(normalized)) {
            const balance = panel.dataset.balance || "0 FCFA";
            return /^0/.test(balance)
                ? `Votre compte est à jour${name}, aucun montant ne reste à payer.`
                : `Le montant restant à payer est de ${balance}${name}. Les détails de vos règlements sont disponibles dans votre ticket.`;
        }

        if (/\b(ticket|commande|suivi|traitement|depot)\b/.test(normalized)) {
            return ticketCount === 0
                ? `Je ne vois pas encore de ticket dans votre espace${name}. Contactez le pressing si vous pensez qu’un dépôt devrait apparaître.`
                : `Vous avez ${ticketCount} ticket${ticketCount > 1 ? "s" : ""} enregistré${ticketCount > 1 ? "s" : ""}, dont ${activeCount} en cours de traitement. ${readyCount > 0 ? `${readyCount} article${readyCount > 1 ? "s sont" : " est"} prêt${readyCount > 1 ? "s" : ""}.` : "Je vous préviendrai ici lorsqu’un article sera prêt."}`;
        }

        if (/\b(horaire|heure|adresse|contacter|contact|message|parler|personne|conseiller)\b/.test(normalized)) {
            return `Pour obtenir une réponse précise${name}, écrivez directement à l’équipe Fresh & Care avec le bouton Parler au pressing. Elle pourra vous confirmer les horaires et les modalités de retrait.`;
        }

        return `Je veux vous répondre correctement${name}, mais je n’ai pas compris cette demande. Vous pouvez me demander le statut d’un ticket, les articles prêts ou votre solde, ou écrire à l’équipe du pressing.`;
    }

    panel.querySelector("[data-voice-welcome]")?.addEventListener("click", () => {
        response.textContent = greeting;
        speak(greeting);
    });

    panel.querySelector("[data-voice-question-form]")?.addEventListener("submit", event => {
        event.preventDefault();
        const input = event.currentTarget.querySelector("input");
        const question = input.value.trim();
        if (!question) {
            response.textContent = "Je vous écoute. Écrivez votre question, puis sélectionnez Demander.";
            input.focus();
            return;
        }
        const answer = answerQuestion(question);
        response.textContent = answer;
        speak(answer);
        input.value = "";
    });

    panel.querySelector("[data-voice-say-form]")?.addEventListener("submit", event => {
        event.preventDefault();
        const input = event.currentTarget.querySelector("input");
        const phrase = input.value.trim();
        if (!phrase) {
            response.textContent = "Écrivez une phrase et je la prononcerai pour vous sur cet appareil.";
            input.focus();
            return;
        }
        response.textContent = `Je prononce : « ${phrase} »`;
        speak(phrase);
        input.value = "";
    });

    panel.querySelector("[data-voice-stop]")?.addEventListener("click", () => {
        if (speechAvailable) window.speechSynthesis.cancel();
        response.textContent = "La voix est arrêtée. Je reste disponible si vous avez une autre question.";
    });
})();
