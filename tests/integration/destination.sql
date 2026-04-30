create table public.accounts (
    id integer primary key,
    email text not null,
    display_name text,
    created_at timestamptz not null
);

create table public.orders (
    id integer primary key,
    account_id integer not null references public.accounts(id),
    total_cents integer not null,
    note text
);
