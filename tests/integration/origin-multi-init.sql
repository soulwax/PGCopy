-- File: tests/integration/origin-multi-init.sql
--
-- Second docker-entrypoint-initdb.d script for the origin-multi container.
-- Runs after origin.sql (which creates the default pgcopy database's
-- accounts/orders tables) and adds two extra databases beyond pgcopy,
-- each with a small distinct table so row counts can be told apart when
-- verifying --all-databases copied every database correctly.

CREATE DATABASE app_one;
CREATE DATABASE app_two;

\connect app_one

create table public.widgets (
    id serial primary key,
    name text not null
);

insert into public.widgets (name) values
    ('alpha'),
    ('beta'),
    ('gamma');

\connect app_two

create table public.gadgets (
    id serial primary key,
    label text not null
);

insert into public.gadgets (label) values
    ('one'),
    ('two');
