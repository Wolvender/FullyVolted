-- Run ONCE as root. Creates the database and the account the API logs in with.
-- Don't edit this template: Claude generates create_user.local.sql (gitignored) with a real password,
-- the same one that is in Api/.env.
--
--   mysql -u root -p -e "source Database/mysql/create_user.local.sql"

create database if not exists fullyvolted character set utf8mb4 collate utf8mb4_unicode_ci;

-- 'localhost' only: this account can't log in from another machine.
create user if not exists 'fullyvolted_api'@'localhost' identified by 'CHANGE_ME';
alter user 'fullyvolted_api'@'localhost' identified by 'CHANGE_ME';

-- Full rights, but only inside the fullyvolted database - nothing else on this MySQL server.
-- (It needs create/alter so init_db.py can build the tables.)
grant all privileges on fullyvolted.* to 'fullyvolted_api'@'localhost';
flush privileges;
