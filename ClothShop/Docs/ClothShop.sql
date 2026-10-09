

IF DB_ID(N'ClothingShop') IS NOT NULL
BEGIN
    ALTER DATABASE ClothingShop SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ClothingShop;
END
GO

CREATE DATABASE ClothingShop;
GO

USE ClothingShop;
GO


CREATE TABLE roles (
    id   INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE clothing_types (
    id   INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE sizes (
    id           INT IDENTITY(1,1) PRIMARY KEY,
    letter_code  NVARCHAR(10) NOT NULL UNIQUE,   -- S, M, L, XL
    numeric_code INT          NOT NULL UNIQUE    -- 44, 46, 48, 50
);

CREATE TABLE order_statuses (
    id   INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(50) NOT NULL UNIQUE
);


CREATE TABLE users (
    id               INT IDENTITY(1,1) PRIMARY KEY,
    login            NVARCHAR(50)  NOT NULL UNIQUE,
    password_hash    NVARCHAR(255) NOT NULL,     -- только хеш, не открытый пароль
    phone            NVARCHAR(20)  NOT NULL,
    delivery_address NVARCHAR(255) NOT NULL,
    role_id          INT           NOT NULL,
    CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES roles(id)
);

CREATE TABLE clothing (
    id             INT IDENTITY(1,1) PRIMARY KEY,
    name           NVARCHAR(150)  NOT NULL,
    price          DECIMAL(10,2)  NOT NULL CHECK (price >= 0),
    description    NVARCHAR(MAX)  NOT NULL DEFAULT N'',
    purchase_count INT            NOT NULL DEFAULT 0 CHECK (purchase_count >= 0),
    type_id        INT            NOT NULL,
    is_available   BIT            NOT NULL DEFAULT 1,   -- доступен ли для продажи
    CONSTRAINT FK_clothing_types FOREIGN KEY (type_id) REFERENCES clothing_types(id)
);

CREATE TABLE clothing_sizes (
    id             INT IDENTITY(1,1) PRIMARY KEY,
    clothing_id    INT NOT NULL,
    size_id        INT NOT NULL,
    stock_quantity INT NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
    CONSTRAINT FK_cs_clothing FOREIGN KEY (clothing_id) REFERENCES clothing(id) ON DELETE CASCADE,
    CONSTRAINT FK_cs_sizes    FOREIGN KEY (size_id)     REFERENCES sizes(id),
    CONSTRAINT UQ_cs_clothing_size UNIQUE (clothing_id, size_id)
);

CREATE TABLE orders (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    created_at DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    status_id  INT       NOT NULL,
    user_id    INT       NOT NULL,
    CONSTRAINT FK_orders_status FOREIGN KEY (status_id) REFERENCES order_statuses(id),
    CONSTRAINT FK_orders_users  FOREIGN KEY (user_id)   REFERENCES users(id)
);

CREATE TABLE order_items (
    id               INT IDENTITY(1,1) PRIMARY KEY,
    order_id         INT           NOT NULL,
    clothing_size_id INT           NOT NULL,
    quantity         INT           NOT NULL CHECK (quantity > 0),
    unit_price       DECIMAL(10,2) NOT NULL CHECK (unit_price >= 0),  -- цена на момент покупки
    CONSTRAINT FK_oi_orders FOREIGN KEY (order_id)         REFERENCES orders(id) ON DELETE CASCADE,
    CONSTRAINT FK_oi_cs     FOREIGN KEY (clothing_size_id) REFERENCES clothing_sizes(id),
    CONSTRAINT UQ_oi_order_cs UNIQUE (order_id, clothing_size_id)
);
GO

CREATE INDEX IX_clothing_name ON clothing (name);          -- поиск по названию
CREATE INDEX IX_clothing_type ON clothing (type_id);
CREATE INDEX IX_orders_status ON orders (status_id);       -- заказы по статусу
CREATE INDEX IX_orders_user   ON orders (user_id);         -- заказы покупателя
GO


INSERT INTO roles (name) VALUES (N'Manager'), (N'User');

INSERT INTO clothing_types (name) VALUES
    (N'Футболка'), (N'Худи'), (N'Свитшот'), (N'Лонгслив');

INSERT INTO sizes (letter_code, numeric_code) VALUES
    (N'XS', 42), (N'S', 44), (N'M', 46), (N'L', 48), (N'XL', 50), (N'XXL', 52);

INSERT INTO order_statuses (name) VALUES
    (N'Создан'), (N'Оплачен'), (N'Отправлен'), (N'Доставлен'), (N'Отменён');

-- Пароли: manager1 / manager123, ivan / ivan123, anna / anna123, petr / petr123
-- Хеши bcrypt (cost 11), совместимы с BCrypt.Net
INSERT INTO users (login, password_hash, phone, delivery_address, role_id) VALUES
    (N'manager1', N'$2b$11$/H2lYdJmU79.YRG6qnGlC.jxk.w8xtWPzJu89viU3xOT6tOPhPEIK',
        N'+7 900 111-11-11', N'г. Москва, ул. Складская, д. 1',
        (SELECT id FROM roles WHERE name = N'Manager')),
    (N'ivan',     N'$2b$11$64Neo/lr0nrQHL/Emh6K/uv7b6W.uLpUxrCMU6xm3gyCuB5OUQy.q',
        N'+7 900 222-22-22', N'г. Москва, ул. Ленина, д. 10, кв. 5',
        (SELECT id FROM roles WHERE name = N'User')),
    (N'anna',     N'$2b$11$P0cydQIOxXXfhJ0isWdr7O6pJQ8L0m.uL9RtomIshyuxLD61mIFrG',
        N'+7 900 333-33-33', N'г. Санкт-Петербург, Невский пр., д. 20, кв. 12',
        (SELECT id FROM roles WHERE name = N'User')),
    (N'petr',     N'$2b$11$Icw0jug.ELnVm99jt2cMLO.iENqUxvQOwKmWvOU0xBvHulGEq.rdm',
        N'+7 900 444-44-44', N'г. Казань, ул. Баумана, д. 7, кв. 3',
        (SELECT id FROM roles WHERE name = N'User'));

INSERT INTO clothing (name, price, description, type_id, is_available) VALUES
    (N'Футболка Basic белая',     990.00,  N'Классическая белая футболка из 100% хлопка',
        (SELECT id FROM clothing_types WHERE name = N'Футболка'), 1),
    (N'Футболка Basic чёрная',    990.00,  N'Классическая чёрная футболка из 100% хлопка',
        (SELECT id FROM clothing_types WHERE name = N'Футболка'), 1),
    (N'Футболка Oversize серая',  1490.00, N'Свободный крой, плотный хлопок 220 г/м²',
        (SELECT id FROM clothing_types WHERE name = N'Футболка'), 1),
    (N'Футболка с принтом Space', 1690.00, N'Футболка с принтом космоса, лимитированная серия',
        (SELECT id FROM clothing_types WHERE name = N'Футболка'), 1),
    (N'Футболка Polo синяя',      1990.00, N'Футболка-поло с воротником',
        (SELECT id FROM clothing_types WHERE name = N'Футболка'), 0),
    (N'Худи Classic графит',      3490.00, N'Тёплое худи с капюшоном и карманом-кенгуру',
        (SELECT id FROM clothing_types WHERE name = N'Худи'), 1),
    (N'Свитшот Urban бежевый',    2790.00, N'Свитшот с начёсом, прямой крой',
        (SELECT id FROM clothing_types WHERE name = N'Свитшот'), 1),
    (N'Лонгслив Sport чёрный',    1790.00, N'Лонгслив для тренировок, дышащая ткань',
        (SELECT id FROM clothing_types WHERE name = N'Лонгслив'), 1);

INSERT INTO clothing_sizes (clothing_id, size_id, stock_quantity)
SELECT c.id, s.id, v.qty
FROM (VALUES
    (N'Футболка Basic белая',     N'S',  15), (N'Футболка Basic белая',     N'M',  25),
    (N'Футболка Basic белая',     N'L',  20), (N'Футболка Basic белая',     N'XL', 10),

    (N'Футболка Basic чёрная',    N'S',  12), (N'Футболка Basic чёрная',    N'M',  30),
    (N'Футболка Basic чёрная',    N'L',  18), (N'Футболка Basic чёрная',    N'XL',  8),

    (N'Футболка Oversize серая',  N'M',  14), (N'Футболка Oversize серая',  N'L',  16),
    (N'Футболка Oversize серая',  N'XL',  9), (N'Футболка Oversize серая',  N'XXL', 5),

    (N'Футболка с принтом Space', N'S',   6), (N'Футболка с принтом Space', N'M',  10),
    (N'Футболка с принтом Space', N'L',   0),

    (N'Футболка Polo синяя',      N'M',   4), (N'Футболка Polo синяя',      N'L',   3),

    (N'Худи Classic графит',      N'M',   7), (N'Худи Classic графит',      N'L',   9),
    (N'Худи Classic графит',      N'XL',  4),

    (N'Свитшот Urban бежевый',    N'S',   5), (N'Свитшот Urban бежевый',    N'M',   8),
    (N'Свитшот Urban бежевый',    N'L',   6),

    (N'Лонгслив Sport чёрный',    N'M',  11), (N'Лонгслив Sport чёрный',    N'L',  13)
) AS v(clothing_name, size_letter, qty)
JOIN clothing c ON c.name        = v.clothing_name
JOIN sizes    s ON s.letter_code = v.size_letter;

INSERT INTO orders (created_at, status_id, user_id) VALUES
    ('2026-09-20T10:15:00', (SELECT id FROM order_statuses WHERE name = N'Доставлен'), (SELECT id FROM users WHERE login = N'ivan')),
    ('2026-09-28T14:40:00', (SELECT id FROM order_statuses WHERE name = N'Отправлен'), (SELECT id FROM users WHERE login = N'anna')),
    ('2026-10-02T09:05:00', (SELECT id FROM order_statuses WHERE name = N'Оплачен'),   (SELECT id FROM users WHERE login = N'ivan')),
    ('2026-10-05T18:20:00', (SELECT id FROM order_statuses WHERE name = N'Создан'),    (SELECT id FROM users WHERE login = N'petr')),
    ('2026-10-06T12:00:00', (SELECT id FROM order_statuses WHERE name = N'Отменён'),   (SELECT id FROM users WHERE login = N'anna'));

INSERT INTO order_items (order_id, clothing_size_id, quantity, unit_price)
SELECT v.order_id, cs.id, v.qty, c.price
FROM (VALUES
    (1, N'Футболка Basic белая',     N'M',  2),
    (1, N'Футболка Basic чёрная',    N'L',  1),

    (2, N'Худи Classic графит',      N'M',  1),
    (2, N'Футболка Oversize серая',  N'L',  1),

    (3, N'Футболка с принтом Space', N'M',  2),

    (4, N'Свитшот Urban бежевый',    N'S',  1),
    (4, N'Лонгслив Sport чёрный',    N'M',  1),
    (4, N'Футболка Basic чёрная',    N'M',  3),

    (5, N'Футболка Oversize серая',  N'XL', 1)
) AS v(order_id, clothing_name, size_letter, qty)
JOIN clothing       c  ON c.name        = v.clothing_name
JOIN sizes          s  ON s.letter_code = v.size_letter
JOIN clothing_sizes cs ON cs.clothing_id = c.id AND cs.size_id = s.id;

UPDATE c
SET purchase_count = ISNULL((
    SELECT SUM(oi.quantity)
    FROM order_items oi
    JOIN clothing_sizes cs ON cs.id = oi.clothing_size_id
    JOIN orders o          ON o.id  = oi.order_id
    JOIN order_statuses st ON st.id = o.status_id
    WHERE cs.clothing_id = c.id
      AND st.name <> N'Отменён'
), 0)
FROM clothing c;
GO
