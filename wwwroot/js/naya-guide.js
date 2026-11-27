(() => {
    const guide = document.querySelector("[data-naya-guide]");
    if (!guide) return;

    const toggle = guide.querySelector("[data-naya-guide-toggle]");
    const toggleText = guide.querySelector("[data-naya-guide-toggle-text]");
    const panel = guide.querySelector("[data-naya-guide-panel]");
    const title = guide.querySelector("[data-naya-guide-title]");
    const text = guide.querySelector("[data-naya-guide-text]");
    const heading = guide.querySelector("[data-naya-guide-heading]");
    const speakButton = guide.querySelector("[data-naya-guide-speak]");
    const stopButton = guide.querySelector("[data-naya-guide-stop]");
    const status = guide.querySelector("[data-naya-guide-status]");
    const canSpeak = "speechSynthesis" in window && "SpeechSynthesisUtterance" in window;

    const help = {
        fr: {
            heading: "Naya · guide de la page",
            open: "Besoin d’aide ?",
            close: "Masquer le guide",
            listen: "🔊 Écouter les conseils",
            stop: "Arrêter",
            speaking: "Naya explique cette étape…",
            finished: "Explication terminée.",
            unavailable: "La voix française n’est pas disponible sur cet appareil. Le conseil reste affiché.",
            pages: {
                "Dashboard.Index": ["Vue d’ensemble", "Les indicateurs en haut donnent l’état immédiat du pressing : clients, tickets, articles prêts, impayés et caisse. Les raccourcis vous guident du dépôt au suivi atelier, puis à l’information du client. Les alertes et le graphique aident à repérer rapidement ce qui demande votre attention."],
                "Dashboard.ClosingReport": ["Rapport Z · clôture de caisse", "Choisissez la date pour consulter les encaissements et les mouvements de caisse. Le rapport distingue espèces, Mobile Money, carte et virement, puis calcule le solde espèces théorique. Vous pouvez l’imprimer pour garder une trace de la clôture."],
                "Clients.Index": ["Gestion des clients", "Recherchez une fiche par nom ou téléphone pour retrouver rapidement les coordonnées, l’historique des dépôts et le solde. Une fiche unique évite de disperser les tickets d’un même client. Utilisez Nouveau client pour enregistrer une personne."],
                "Clients.Create": ["Créer une fiche client", "Saisissez le nom et un numéro de téléphone fiable; ajoutez WhatsApp ou e-mail si le client souhaite être contacté par ces moyens. La fiche rassemble ses dépôts, paiements et messages. Après l’enregistrement, un code d’accès client est généré : remettez-le discrètement à la personne."],
                "Clients.Edit": ["Mettre à jour une fiche", "Corrigez les coordonnées afin que les reçus, les échanges et le suivi restent associés à la bonne personne. Les changements ne suppriment pas les tickets ni l’historique financier."],
                "Clients.Details": ["Dossier client", "Cette page réunit les coordonnées, les tickets, l’historique et le solde du client. Elle permet de reprendre son suivi sans rechercher chaque information séparément."],
                "Orders.Index": ["Tickets et articles", "Consultez les dépôts et l’avancement des articles pour repérer les commandes à traiter ou à remettre. Ouvrez un ticket pour vérifier les détails, modifier un statut et suivre les paiements."],
                "Orders.Create": ["Enregistrer un dépôt", "Choisissez la fiche du client, puis ajoutez chaque vêtement comme un article distinct. Indiquez la catégorie, l’état, le tarif et la date de retrait; le total se met à jour avant la validation. Le ticket conserve le détail du dépôt et facilite le suivi en atelier."],
                "Orders.Details": ["Suivi du ticket et de l’atelier", "Mettez à jour le statut de chaque article et son emplacement pour que l’équipe sache où le retrouver. La date de retrait et les paiements restent visibles sur le même écran. Marquez un article prêt seulement après avoir terminé son traitement."],
                "Communications.Index": ["Messagerie du pressing", "La boîte rassemble les échanges clients et signale les conversations non lues. Répondez depuis le fil correspondant pour conserver le contexte. Les notifications directes par e-mail demandent des paramètres SMTP valides; WhatsApp peut préparer un message à vérifier avant l’envoi."],
                "Communications.Compose": ["Composer un message", "Vérifiez le destinataire et le texte avant l’envoi. Les messages conservés dans l’historique facilitent le suivi des demandes. Pour l’e-mail direct, le compte SMTP doit être correctement configuré."],
                "Communications.Thread": ["Répondre à une conversation", "Relisez les messages précédents pour garder une réponse cohérente avec la demande du client. Répondez dans ce fil : toute l’équipe pourra retrouver l’échange dans l’historique."],
                "Inventory.Index": ["Stock et consommables", "Enregistrez les quantités et les seuils de réapprovisionnement pour savoir quand commander de nouveaux produits. Les alertes mettent en évidence les articles qui atteignent leur seuil et aident à éviter une rupture pendant le travail."],
                "Tariffs.Index": ["Grille tarifaire", "Mettez à jour les prix par catégorie et les majorations express pour que le calcul des prochains dépôts corresponde à vos tarifs. Vérifiez les montants approuvés avant de les appliquer aux factures."],
                "default": ["Votre espace Fresh & Care", "Utilisez le menu de gauche pour retrouver les clients, les tickets, la messagerie, le stock, les tarifs et les rapports. Naya explique les étapes et les avantages de chaque écran pour vous accompagner pendant la saisie."]
            }
        },
        en: {
            heading: "Naya · page guide",
            open: "Need help?",
            close: "Hide guide",
            listen: "🔊 Listen to the tips",
            stop: "Stop",
            speaking: "Naya is explaining this step…",
            finished: "Explanation complete.",
            unavailable: "English speech is unavailable on this device. The guidance remains visible.",
            pages: {
                "Dashboard.Index": ["Overview", "The cards at the top show the pressing’s current position: customers, active tickets, ready items, outstanding balances and cash. The shortcuts guide you from drop-off to workshop tracking and customer updates. Alerts and the chart help you spot what needs attention."],
                "Dashboard.ClosingReport": ["Daily cash closing · Z report", "Choose a date to review payments and cash movements. The report separates cash, mobile money, cards and transfers, then calculates the expected cash balance. Print it to keep a record of the closing."],
                "Clients.Index": ["Customer management", "Search by name or phone to find contact details, drop-off history and balance quickly. A single customer record keeps their tickets together. Select New client to create a record."],
                "Clients.Create": ["Create a customer record", "Enter the customer’s name and a reliable phone number. Add WhatsApp or email if they want to be contacted that way. The record brings together their drop-offs, payments and messages. After saving, give the generated customer access code to them privately."],
                "Clients.Edit": ["Update a customer record", "Correct the contact details so receipts, conversations and tracking stay linked to the right person. Updating details does not remove tickets or payment history."],
                "Clients.Details": ["Customer record", "This page brings together contact details, tickets, history and balance. You can continue helping the customer without searching separate pages for each detail."],
                "Orders.Index": ["Tickets and garments", "Review drop-offs and garment progress to find orders that need processing or collection. Open a ticket to check details, update a status and review payments."],
                "Orders.Create": ["Record a drop-off", "Choose the customer record, then add each garment as a separate item. Enter its category, condition, price and expected collection date; the total updates before you save. The ticket keeps the drop-off details together for workshop tracking."],
                "Orders.Details": ["Ticket and workshop tracking", "Update each garment’s status and location so the team can find it. The collection date and payments remain visible on the same page. Mark a garment ready only after its treatment is complete."],
                "Communications.Index": ["Pressing inbox", "The inbox brings customer conversations together and flags unread messages. Reply in the matching thread to keep the context. Direct email requires valid SMTP settings; WhatsApp can prepare a message for you to review before sending."],
                "Communications.Compose": ["Compose a message", "Check the recipient and message before sending. Keeping messages in the history makes follow-up easier. Direct email requires a correctly configured SMTP account."],
                "Communications.Thread": ["Reply to a conversation", "Read earlier messages so your reply fits the customer’s request. Reply in this thread so the team can find the conversation in its history."],
                "Inventory.Index": ["Stock and supplies", "Record quantities and reorder thresholds to know when to replenish supplies. Alerts highlight items at their threshold and help avoid running out during work."],
                "Tariffs.Index": ["Price list", "Update category prices and express surcharges so new drop-offs use the correct rates. Confirm approved prices before applying them to invoices."],
                "default": ["Your Fresh & Care workspace", "Use the left menu to open customers, tickets, messages, stock, pricing and reports. Naya explains each screen and its benefits to help while you enter information."]
            }
        }
    };

    let language = "fr";
    try { language = window.localStorage.getItem("freshcare.naya.language") === "en" ? "en" : "fr"; } catch { }

    const words = help[language];
    const key = `${guide.dataset.controller}.${guide.dataset.action}`;
    const guidance = words.pages[key] || words.pages.default;
    heading.textContent = words.heading;
    toggleText.textContent = words.open;
    title.textContent = guidance[0];
    text.textContent = guidance[1];
    speakButton.textContent = words.listen;
    stopButton.textContent = words.stop;
    guide.setAttribute("lang", language);

    function openGuide(open) {
        panel.hidden = !open;
        toggle.setAttribute("aria-expanded", String(open));
        toggleText.textContent = open ? words.close : words.open;
    }

    toggle.addEventListener("click", () => openGuide(panel.hidden));
    guide.querySelector("[data-naya-guide-close]")?.addEventListener("click", () => openGuide(false));
    if (guide.dataset.autoOpen === "true") openGuide(true);

    speakButton.addEventListener("click", () => {
        if (!canSpeak) {
            status.textContent = words.unavailable;
            return;
        }
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(`${guidance[0]}. ${guidance[1]}`);
        utterance.lang = language === "fr" ? "fr-FR" : "en-US";
        utterance.rate = 0.95;
        const prefix = language === "fr" ? "fr" : "en";
        const voice = window.speechSynthesis.getVoices().find(item => item.lang?.toLowerCase().startsWith(prefix));
        if (voice) utterance.voice = voice;
        speakButton.disabled = true;
        stopButton.disabled = false;
        status.textContent = words.speaking;
        utterance.onend = () => {
            speakButton.disabled = false;
            stopButton.disabled = true;
            status.textContent = words.finished;
        };
        utterance.onerror = event => {
            speakButton.disabled = false;
            stopButton.disabled = true;
            status.textContent = event.error === "canceled" || event.error === "interrupted" ? "" : words.unavailable;
        };
        window.speechSynthesis.speak(utterance);
    });

    stopButton.addEventListener("click", () => {
        if (canSpeak) window.speechSynthesis.cancel();
        speakButton.disabled = false;
        stopButton.disabled = true;
        status.textContent = "";
    });
})();
