let currentPage = 1;
let pageSize = 20;
let totalTickets = 0;

async function login() {
    const username = document.getElementById("username").value;
    const password = document.getElementById("password").value;

    const response = await fetch("/api/login", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            username,
            password
        })
    });

    if (!response.ok) {
        document.getElementById("loginMessage").textContent =
            "Неверный логин или пароль";
        return;
    }

    const user = await response.json();

    document.getElementById("currentUser").textContent =
        `${user.fullName} (${user.username})`;

    document.getElementById("loginSection").classList.add("hidden");
    document.getElementById("appSection").classList.remove("hidden");

    await loadCategories();
    await loadTickets();
    await loadSummary();
}

async function logout() {
    await fetch("/api/logout", {
        method: "POST"
    });

    location.reload();
}

async function loadCategories() {
    const response = await fetch("/api/categories");

    if (!response.ok) {
        return;
    }

    const categories = await response.json();

    const createSelect =
        document.getElementById("ticketCategory");

    const filterSelect =
        document.getElementById("categoryFilter");

    createSelect.innerHTML = "";
    filterSelect.innerHTML =
        '<option value="">Все категории</option>';

    for (const category of categories) {
        const option1 = document.createElement("option");

        option1.value = category.id;
        option1.textContent =
            `${category.name} (${category.reactionTimeMinutes} мин.)`;

        createSelect.appendChild(option1);

        const option2 = document.createElement("option");

        option2.value = category.id;
        option2.textContent = category.name;

        filterSelect.appendChild(option2);
    }
}

async function createTicket() {
    const title =
        document.getElementById("ticketTitle").value;

    const description =
        document.getElementById("ticketDescription").value;

    const categoryId =
        Number(document.getElementById("ticketCategory").value);

    const response = await fetch("/api/tickets", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            title,
            description,
            categoryId
        })
    });

    if (!response.ok) {
        document.getElementById("createMessage").textContent =
            "Ошибка при создании заявки";
        return;
    }

    document.getElementById("createMessage").textContent =
        "Заявка создана";

    document.getElementById("ticketTitle").value = "";
    document.getElementById("ticketDescription").value = "";

    currentPage = 1;

    await loadTickets();
    await loadSummary();
}

async function loadTickets() {
    const status =
        document.getElementById("statusFilter").value;

    const categoryId =
        document.getElementById("categoryFilter").value;

    const params = new URLSearchParams();

    params.set("page", currentPage);
    params.set("size", pageSize);

    if (status) {
        params.set("status", status);
    }

    if (categoryId) {
        params.set("categoryId", categoryId);
    }

    const response =
        await fetch(`/api/tickets?${params}`);

    if (!response.ok) {
        return;
    }

    const data = await response.json();

    totalTickets = data.total;

    const body =
        document.getElementById("ticketsBody");

    body.innerHTML = "";

    for (const ticket of data.items) {
        const row = document.createElement("tr");

        row.innerHTML = `
            <td>${ticket.id}</td>
            <td>${ticket.title}</td>
            <td>${formatStatus(ticket.status)}</td>
            <td>${ticket.category.name}</td>
            <td>${ticket.user.fullName}</td>
            <td>${ticket.commentsCount}</td>
            <td>
                <button onclick="openTicket(${ticket.id})">
                    Открыть
                </button>
            </td>
        `;

        body.appendChild(row);
    }

    const pages =
        Math.max(1, Math.ceil(totalTickets / pageSize));

    document.getElementById("pageInfo").textContent =
        `Страница ${currentPage} из ${pages}. Всего: ${totalTickets}`;
}

function previousPage() {
    if (currentPage > 1) {
        currentPage--;
        loadTickets();
    }
}

function nextPage() {
    const pages =
        Math.ceil(totalTickets / pageSize);

    if (currentPage < pages) {
        currentPage++;
        loadTickets();
    }
}

async function openTicket(id) {
    const response =
        await fetch(`/api/tickets/${id}`);

    if (!response.ok) {
        return;
    }

    const ticket = await response.json();

    const meResponse = await fetch("/api/me");
    const me = await meResponse.json();

    const isEmployee =
        String(me.isEmployee).toLowerCase() === "true";

    const card =
        document.getElementById("ticketCard");

    const details =
        document.getElementById("ticketDetails");

    let commentsHtml = "";

    for (const comment of ticket.comments) {
        commentsHtml += `
            <div class="comment">
                <strong>${comment.user.fullName}</strong><br>
                ${comment.text}<br>
                <small>${formatDate(comment.createdAt)}</small>
            </div>
        `;
    }

    let employeeControls = "";

    if (isEmployee) {
        employeeControls = `
            <hr>

            <h3>Ответ сотрудника</h3>

            <textarea
                id="newCommentText"
                placeholder="Введите комментарий"></textarea>

            <button onclick="addComment(${ticket.id})">
                Добавить комментарий
            </button>

            <p id="commentMessage"></p>

            <h3>Изменить статус</h3>

            <select id="newTicketStatus">
                <option value="new"
                    ${ticket.status === "new" ? "selected" : ""}>
                    Новая
                </option>

                <option value="in_progress"
                    ${ticket.status === "in_progress" ? "selected" : ""}>
                    В работе
                </option>

                <option value="closed"
                    ${ticket.status === "closed" ? "selected" : ""}>
                    Закрыта
                </option>
            </select>

            <button onclick="changeTicketStatus(${ticket.id})">
                Изменить статус
            </button>

            <p id="statusMessage"></p>
        `;
    }

    details.innerHTML = `
        <div class="ticket-card">

            <p><strong>ID:</strong> ${ticket.id}</p>

            <p>
                <strong>Заголовок:</strong>
                ${ticket.title}
            </p>

            <p>
                <strong>Описание:</strong>
                ${ticket.description}
            </p>

            <p>
                <strong>Статус:</strong>
                ${formatStatus(ticket.status)}
            </p>

            <p>
                <strong>Категория:</strong>
                ${ticket.category.name}
            </p>

            <p>
                <strong>Норматив реакции:</strong>
                ${ticket.category.reactionTimeMinutes} мин.
            </p>

            <p>
                <strong>Время реакции:</strong>
                ${
                    ticket.reactionTimeMinutes === null
                    ? "Ответа сотрудника ещё нет"
                    : ticket.reactionTimeMinutes.toFixed(2) + " мин."
                }
            </p>

            <p>
                <strong>Нарушение норматива:</strong>
                ${ticket.slaViolated ? "Да" : "Нет"}
            </p>

            <p>
                <strong>Создана:</strong>
                ${formatDate(ticket.createdAt)}
            </p>

            <p>
                <strong>Закрыта:</strong>
                ${
                    ticket.closedAt
                    ? formatDate(ticket.closedAt)
                    : "Нет"
                }
            </p>

            <h3>Комментарии</h3>

            ${commentsHtml || "<p>Комментариев нет</p>"}

            ${employeeControls}
        </div>
    `;

    card.classList.remove("hidden");

    card.scrollIntoView({
        behavior: "smooth"
    });
}

async function loadSummary() {
    const response =
        await fetch("/api/summary");

    if (!response.ok) {
        return;
    }

    const summary =
        await response.json();

    let categoriesHtml = "";

    for (const category of summary.categories) {
        categoriesHtml += `
            <tr>
                <td>${category.name}</td>
                <td>${category.reactionTimeMinutes}</td>
                <td>${category.openTickets}</td>
            </tr>
        `;
    }

    document.getElementById("summary").innerHTML = `
        <div class="summary-card">

            <p>
                <strong>Всего заявок:</strong>
                ${summary.totalTickets}
            </p>

            <p>
                <strong>Нарушений норматива:</strong>
                ${summary.violatedTickets}
            </p>

            <p>
                <strong>Доля нарушений:</strong>
                ${(summary.violationRate * 100).toFixed(2)} %
            </p>

            <table>
                <thead>
                <tr>
                    <th>Категория</th>
                    <th>Норматив, мин.</th>
                    <th>Открытых заявок</th>
                </tr>
                </thead>

                <tbody>
                    ${categoriesHtml}
                </tbody>
            </table>
        </div>
    `;
}

function formatStatus(status) {
    switch (status) {
        case "new":
            return "Новая";

        case "in_progress":
            return "В работе";

        case "closed":
            return "Закрыта";

        default:
            return status;
    }
}

function formatDate(value) {
    if (!value) {
        return "";
    }

    return new Date(value).toLocaleString("ru-RU");
}

async function addComment(ticketId) {
    const text =
        document.getElementById("newCommentText").value.trim();

    if (!text) {
        document.getElementById("commentMessage").textContent =
            "Введите текст комментария";
        return;
    }

    const response = await fetch(
        `/api/tickets/${ticketId}/comments`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                text
            })
        });

    if (!response.ok) {
        document.getElementById("commentMessage").textContent =
            "Не удалось добавить комментарий";
        return;
    }

    await openTicket(ticketId);
    await loadTickets();
    await loadSummary();
}

async function changeTicketStatus(ticketId) {
    const status =
        document.getElementById("newTicketStatus").value;

    const response = await fetch(
        `/api/tickets/${ticketId}/status`,
        {
            method: "PUT",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                status
            })
        });

    if (!response.ok) {
        document.getElementById("statusMessage").textContent =
            "Не удалось изменить статус";
        return;
    }

    await openTicket(ticketId);
    await loadTickets();
    await loadSummary();
}