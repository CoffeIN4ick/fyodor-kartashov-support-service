import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5212';

const MODE = __ENV.MODE || 'mixed';
const RATE = Number(__ENV.RATE || 1);
const DURATION = __ENV.DURATION || '30s';

const PRE_VUS = Number(__ENV.PRE_VUS || Math.max(10, RATE * 2));
const MAX_VUS = Number(__ENV.MAX_VUS || Math.max(100, RATE * 5));

const TICKET_COUNT = Number(__ENV.TICKET_COUNT || 50000);
const CATEGORY_COUNT = Number(__ENV.CATEGORY_COUNT || 10);

export const options = {
    discardResponseBodies: true,

    scenarios: {
        load: {
            executor: 'constant-arrival-rate',
            rate: RATE,
            timeUnit: '1s',
            duration: DURATION,
            preAllocatedVUs: PRE_VUS,
            maxVUs: MAX_VUS,
            exec: MODE === 'heavy'
                ? 'heavyScenario'
                : 'mixedScenario',
        },
    },

    thresholds: {
        'http_req_duration{load:true}': [
            'p(95)<500',
        ],

        'http_req_failed{load:true}': [
            'rate<0.01',
        ],
    },
};

export function setup() {
    const demo = login('demo', 'demo');
    const employee = login('employee', 'employee');

    return {
        demoCookie: demo,
        employeeCookie: employee,
    };
}

function login(username, password) {
    const body = JSON.stringify({
        username: username,
        password: password,
    });

    const response = http.post(
        `${BASE_URL}/api/login`,
        body,
        {
            headers: {
                'Content-Type': 'application/json',
            },

            tags: {
                load: 'false',
                operation: 'login',
            },
        }
    );

    const ok = check(response, {
        [`login ${username} = 200`]:
            (r) => r.status === 200,
    });

    if (!ok) {
        throw new Error(
            `Не удалось войти как ${username}. HTTP ${response.status}`
        );
    }

    const authCookies =
        response.cookies['fyodor_kartashov_auth'];

    if (!authCookies || authCookies.length === 0) {
        throw new Error(
            `Cookie авторизации для ${username} не получена`
        );
    }

    return `fyodor_kartashov_auth=${authCookies[0].value}`;
}

function params(cookie, operation) {
    return {
        headers: {
            Cookie: cookie,
            'Content-Type': 'application/json',
        },

        tags: {
            load: 'true',
            operation: operation,
        },
    };
}

function randomTicketId() {
    return Math.floor(
        Math.random() * TICKET_COUNT
    ) + 1;
}

function randomCategoryId() {
    return Math.floor(
        Math.random() * CATEGORY_COUNT
    ) + 1;
}

function getMe(data) {
    const response = http.get(
        `${BASE_URL}/api/me`,
        params(data.demoCookie, 'get_me')
    );

    check(response, {
        'GET /api/me = 200':
            (r) => r.status === 200,
    });
}

function getCategories(data) {
    const response = http.get(
        `${BASE_URL}/api/categories`,
        params(data.demoCookie, 'get_categories')
    );

    check(response, {
        'GET /api/categories = 200':
            (r) => r.status === 200,
    });
}

function getTickets(data) {
    const response = http.get(
        `${BASE_URL}/api/tickets?page=1&size=20`,
        params(data.demoCookie, 'get_tickets')
    );

    check(response, {
        'GET /api/tickets = 200':
            (r) => r.status === 200,
    });
}

function getTicket(data) {
    const id = randomTicketId();

    const response = http.get(
        `${BASE_URL}/api/tickets/${id}`,
        params(data.demoCookie, 'get_ticket')
    );

    check(response, {
        'GET /api/tickets/{id} = 200':
            (r) => r.status === 200,
    });
}

function createTicket(data) {
    const body = JSON.stringify({
        title:
            `Нагрузочная заявка ${__VU}-${__ITER}`,

        description:
            'Заявка создана нагрузочным тестом k6.',

        categoryId:
            randomCategoryId(),
    });

    const response = http.post(
        `${BASE_URL}/api/tickets`,
        body,
        params(data.demoCookie, 'create_ticket')
    );

    check(response, {
        'POST /api/tickets = 201':
            (r) => r.status === 201,
    });
}

function addComment(data) {
    const id = randomTicketId();

    const body = JSON.stringify({
        text:
            `Комментарий нагрузочного теста ${__VU}-${__ITER}`,
    });

    const response = http.post(
        `${BASE_URL}/api/tickets/${id}/comments`,
        body,
        params(
            data.employeeCookie,
            'add_comment'
        )
    );

    check(response, {
        'POST comment = 201':
            (r) => r.status === 201,
    });
}

function changeStatus(data) {
    const id = randomTicketId();

    const statuses = [
        'new',
        'in_progress',
        'closed',
    ];

    const status =
        statuses[
            Math.floor(
                Math.random() * statuses.length
            )
        ];

    const body = JSON.stringify({
        status: status,
    });

    const response = http.put(
        `${BASE_URL}/api/tickets/${id}/status`,
        body,
        params(
            data.employeeCookie,
            'change_status'
        )
    );

    check(response, {
        'PUT status = 200':
            (r) => r.status === 200,
    });
}

function getSummary(data) {
    const response = http.get(
        `${BASE_URL}/api/summary`,
        params(data.demoCookie, 'summary')
    );

    check(response, {
        'GET /api/summary = 200':
            (r) => r.status === 200,
    });
}

export function mixedScenario(data) {
    const value = Math.random() * 100;

    if (value < 30) {
        getTickets(data);
    }
    else if (value < 60) {
        getTicket(data);
    }
    else if (value < 72) {
        addComment(data);
    }
    else if (value < 80) {
        changeStatus(data);
    }
    else if (value < 88) {
        createTicket(data);
    }
    else if (value < 93) {
        getCategories(data);
    }
    else if (value < 98) {
        getMe(data);
    }
    else {
        getSummary(data);
    }
}

export function heavyScenario(data) {
    getSummary(data);
}