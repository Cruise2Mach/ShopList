-- ShopList initial database schema
-- Run once in Supabase Dashboard -> SQL Editor.
-- This migration creates the multi-user, multi-list foundation.
--
-- Important:
-- - Authentication is provided by Supabase Auth.
-- - Client access is protected with Row Level Security (RLS).
-- - Sensitive multi-table operations are exposed as SECURITY DEFINER RPC functions.
-- - Realtime Broadcast is intentionally added in a later migration.

begin;

create extension if not exists pgcrypto with schema extensions;

create schema if not exists private;
revoke all on schema private from public;

do $$
begin
    create type public.list_member_role as enum ('owner', 'admin', 'member');
exception
    when duplicate_object then null;
end
$$;

create table if not exists public.profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    display_name text not null
        check (char_length(trim(display_name)) between 1 and 80),
    avatar_url text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.shopping_lists (
    id uuid primary key default gen_random_uuid(),
    name text not null
        check (char_length(trim(name)) between 1 and 100),
    created_by uuid not null references auth.users(id) on delete restrict,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    archived_at timestamptz
);

create table if not exists public.list_members (
    list_id uuid not null references public.shopping_lists(id) on delete cascade,
    user_id uuid not null references auth.users(id) on delete cascade,
    role public.list_member_role not null default 'member',
    joined_at timestamptz not null default now(),
    removed_at timestamptz,
    primary key (list_id, user_id)
);

create table if not exists public.list_invites (
    id uuid primary key default gen_random_uuid(),
    list_id uuid not null references public.shopping_lists(id) on delete cascade,
    token_hash text not null unique,
    created_by uuid not null references auth.users(id) on delete cascade,
    expires_at timestamptz not null,
    max_uses integer not null default 1 check (max_uses between 1 and 100),
    use_count integer not null default 0
        check (use_count >= 0 and use_count <= max_uses),
    created_at timestamptz not null default now(),
    revoked_at timestamptz
);

create table if not exists public.shopping_items (
    id uuid primary key default gen_random_uuid(),
    list_id uuid not null references public.shopping_lists(id) on delete cascade,
    name text not null
        check (char_length(trim(name)) between 1 and 120),
    quantity text
        check (quantity is null or char_length(quantity) <= 60),
    unit text
        check (unit is null or char_length(unit) <= 30),
    note text
        check (note is null or char_length(note) <= 500),
    is_completed boolean not null default false,
    completed_by uuid references auth.users(id) on delete set null,
    completed_at timestamptz,
    created_by uuid not null references auth.users(id) on delete restrict,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    deleted_at timestamptz
);

create index if not exists list_members_active_user_idx
    on public.list_members(user_id, list_id)
    where removed_at is null;

create index if not exists list_members_active_list_idx
    on public.list_members(list_id, user_id, role)
    where removed_at is null;

create index if not exists shopping_lists_created_by_idx
    on public.shopping_lists(created_by);

create index if not exists shopping_items_list_updated_idx
    on public.shopping_items(list_id, updated_at desc);

create index if not exists shopping_items_list_deleted_idx
    on public.shopping_items(list_id, deleted_at);

create index if not exists list_invites_list_idx
    on public.list_invites(list_id, expires_at);

-- ---------------------------------------------------------------------------
-- Internal authorization helpers
-- ---------------------------------------------------------------------------

create or replace function private.is_list_member(
    target_list_id uuid,
    target_user_id uuid
)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.list_members lm
        where lm.list_id = target_list_id
          and lm.user_id = target_user_id
          and lm.removed_at is null
    );
$$;

create or replace function private.is_list_admin(
    target_list_id uuid,
    target_user_id uuid
)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.list_members lm
        where lm.list_id = target_list_id
          and lm.user_id = target_user_id
          and lm.removed_at is null
          and lm.role in ('owner', 'admin')
    );
$$;

create or replace function private.is_list_owner(
    target_list_id uuid,
    target_user_id uuid
)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.list_members lm
        where lm.list_id = target_list_id
          and lm.user_id = target_user_id
          and lm.removed_at is null
          and lm.role = 'owner'
    );
$$;

create or replace function private.shares_active_list(
    target_user_id uuid,
    viewer_user_id uuid
)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.list_members viewer_membership
        join public.list_members target_membership
          on target_membership.list_id = viewer_membership.list_id
        where viewer_membership.user_id = viewer_user_id
          and viewer_membership.removed_at is null
          and target_membership.user_id = target_user_id
          and target_membership.removed_at is null
    );
$$;

grant usage on schema private to authenticated;
grant execute on function private.is_list_member(uuid, uuid) to authenticated;
grant execute on function private.is_list_admin(uuid, uuid) to authenticated;
grant execute on function private.is_list_owner(uuid, uuid) to authenticated;
grant execute on function private.shares_active_list(uuid, uuid) to authenticated;

-- ---------------------------------------------------------------------------
-- Common triggers
-- ---------------------------------------------------------------------------

create or replace function public.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    new.updated_at := now();
    return new;
end;
$$;

drop trigger if exists profiles_set_updated_at on public.profiles;
create trigger profiles_set_updated_at
before update on public.profiles
for each row execute function public.set_updated_at();

drop trigger if exists shopping_lists_set_updated_at on public.shopping_lists;
create trigger shopping_lists_set_updated_at
before update on public.shopping_lists
for each row execute function public.set_updated_at();

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    chosen_name text;
begin
    chosen_name := coalesce(
        nullif(trim(new.raw_user_meta_data ->> 'display_name'), ''),
        nullif(split_part(coalesce(new.email, ''), '@', 1), ''),
        'User'
    );

    insert into public.profiles (id, display_name)
    values (new.id, left(chosen_name, 80));

    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
after insert on auth.users
for each row execute function public.handle_new_user();

create or replace function public.prepare_shopping_item()
returns trigger
language plpgsql
set search_path = ''
as $$
declare
    current_user_id uuid := (select auth.uid());
begin
    if current_user_id is null then
        raise exception 'Authentication required';
    end if;

    new.name := trim(new.name);
    new.quantity := nullif(trim(new.quantity), '');
    new.unit := nullif(trim(new.unit), '');
    new.note := nullif(trim(new.note), '');

    if tg_op = 'INSERT' then
        new.created_by := current_user_id;
        new.created_at := now();
    else
        if new.id is distinct from old.id
           or new.list_id is distinct from old.list_id
           or new.created_by is distinct from old.created_by
           or new.created_at is distinct from old.created_at then
            raise exception 'Immutable shopping item fields cannot be changed';
        end if;
    end if;

    if new.is_completed then
        if tg_op = 'INSERT' or old.is_completed is false then
            new.completed_by := current_user_id;
            new.completed_at := now();
        end if;
    else
        new.completed_by := null;
        new.completed_at := null;
    end if;

    new.updated_at := now();
    return new;
end;
$$;

drop trigger if exists shopping_items_prepare on public.shopping_items;
create trigger shopping_items_prepare
before insert or update on public.shopping_items
for each row execute function public.prepare_shopping_item();

-- ---------------------------------------------------------------------------
-- Row Level Security
-- ---------------------------------------------------------------------------

alter table public.profiles enable row level security;
alter table public.shopping_lists enable row level security;
alter table public.list_members enable row level security;
alter table public.list_invites enable row level security;
alter table public.shopping_items enable row level security;

drop policy if exists "profiles_select_shared" on public.profiles;
create policy "profiles_select_shared"
on public.profiles
for select
to authenticated
using (
    (select auth.uid()) is not null
    and (
        id = (select auth.uid())
        or (select private.shares_active_list(id, (select auth.uid())))
    )
);

drop policy if exists "profiles_update_self" on public.profiles;
create policy "profiles_update_self"
on public.profiles
for update
to authenticated
using (id = (select auth.uid()))
with check (id = (select auth.uid()));

drop policy if exists "shopping_lists_select_member" on public.shopping_lists;
create policy "shopping_lists_select_member"
on public.shopping_lists
for select
to authenticated
using (
    (select private.is_list_member(id, (select auth.uid())))
);

drop policy if exists "list_members_select_member" on public.list_members;
create policy "list_members_select_member"
on public.list_members
for select
to authenticated
using (
    (select private.is_list_member(list_id, (select auth.uid())))
);

drop policy if exists "list_invites_select_admin" on public.list_invites;
create policy "list_invites_select_admin"
on public.list_invites
for select
to authenticated
using (
    (select private.is_list_admin(list_id, (select auth.uid())))
);

drop policy if exists "shopping_items_select_member" on public.shopping_items;
create policy "shopping_items_select_member"
on public.shopping_items
for select
to authenticated
using (
    (select private.is_list_member(list_id, (select auth.uid())))
);

drop policy if exists "shopping_items_insert_member" on public.shopping_items;
create policy "shopping_items_insert_member"
on public.shopping_items
for insert
to authenticated
with check (
    created_by = (select auth.uid())
    and (select private.is_list_member(list_id, (select auth.uid())))
);

drop policy if exists "shopping_items_update_member" on public.shopping_items;
create policy "shopping_items_update_member"
on public.shopping_items
for update
to authenticated
using (
    (select private.is_list_member(list_id, (select auth.uid())))
)
with check (
    (select private.is_list_member(list_id, (select auth.uid())))
);

-- ---------------------------------------------------------------------------
-- RPC: create a list, create an invite, and join with an invite
-- ---------------------------------------------------------------------------

create or replace function public.create_shopping_list(list_name text)
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
    current_user_id uuid := (select auth.uid());
    clean_name text := trim(list_name);
    new_list_id uuid;
begin
    if current_user_id is null then
        raise exception 'Authentication required';
    end if;

    if clean_name is null or char_length(clean_name) not between 1 and 100 then
        raise exception 'List name must contain between 1 and 100 characters';
    end if;

    insert into public.shopping_lists (name, created_by)
    values (clean_name, current_user_id)
    returning id into new_list_id;

    insert into public.list_members (list_id, user_id, role)
    values (new_list_id, current_user_id, 'owner');

    return new_list_id;
end;
$$;

create or replace function public.create_list_invite(
    target_list_id uuid,
    valid_for_hours integer default 168,
    allowed_uses integer default 1
)
returns text
language plpgsql
security definer
set search_path = ''
as $$
declare
    current_user_id uuid := (select auth.uid());
    raw_token text;
    hashed_token text;
begin
    if current_user_id is null then
        raise exception 'Authentication required';
    end if;

    if not private.is_list_admin(target_list_id, current_user_id) then
        raise exception 'Only list owners and admins can create invites';
    end if;

    if valid_for_hours not between 1 and 720 then
        raise exception 'Invite lifetime must be between 1 and 720 hours';
    end if;

    if allowed_uses not between 1 and 100 then
        raise exception 'Invite usage limit must be between 1 and 100';
    end if;

    raw_token := upper(encode(extensions.gen_random_bytes(8), 'hex'));
    hashed_token := encode(
        extensions.digest(lower(raw_token), 'sha256'),
        'hex'
    );

    insert into public.list_invites (
        list_id,
        token_hash,
        created_by,
        expires_at,
        max_uses
    )
    values (
        target_list_id,
        hashed_token,
        current_user_id,
        now() + make_interval(hours => valid_for_hours),
        allowed_uses
    );

    return raw_token;
end;
$$;

create or replace function public.join_list_with_invite(invite_token text)
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
    current_user_id uuid := (select auth.uid());
    hashed_token text;
    matched_invite public.list_invites%rowtype;
    already_active boolean;
begin
    if current_user_id is null then
        raise exception 'Authentication required';
    end if;

    if invite_token is null or trim(invite_token) = '' then
        raise exception 'Invite token is required';
    end if;

    hashed_token := encode(
        extensions.digest(lower(trim(invite_token)), 'sha256'),
        'hex'
    );

    select *
    into matched_invite
    from public.list_invites li
    where li.token_hash = hashed_token
      and li.revoked_at is null
      and li.expires_at > now()
      and li.use_count < li.max_uses
    for update;

    if not found then
        raise exception 'Invite is invalid, expired, revoked, or fully used';
    end if;

    select exists (
        select 1
        from public.list_members lm
        where lm.list_id = matched_invite.list_id
          and lm.user_id = current_user_id
          and lm.removed_at is null
    )
    into already_active;

    if already_active then
        return matched_invite.list_id;
    end if;

    insert into public.list_members (
        list_id,
        user_id,
        role,
        joined_at,
        removed_at
    )
    values (
        matched_invite.list_id,
        current_user_id,
        'member',
        now(),
        null
    )
    on conflict (list_id, user_id)
    do update set
        joined_at = now(),
        removed_at = null,
        role = case
            when public.list_members.role = 'owner' then 'owner'::public.list_member_role
            else 'member'::public.list_member_role
        end;

    update public.list_invites
    set use_count = use_count + 1
    where id = matched_invite.id;

    return matched_invite.list_id;
end;
$$;

revoke all on function public.create_shopping_list(text) from public, anon;
revoke all on function public.create_list_invite(uuid, integer, integer) from public, anon;
revoke all on function public.join_list_with_invite(text) from public, anon;

grant execute on function public.create_shopping_list(text) to authenticated;
grant execute on function public.create_list_invite(uuid, integer, integer) to authenticated;
grant execute on function public.join_list_with_invite(text) to authenticated;

-- ---------------------------------------------------------------------------
-- API privileges. RLS still decides which rows are visible or writable.
-- ---------------------------------------------------------------------------

revoke all on public.profiles from anon;
revoke all on public.shopping_lists from anon;
revoke all on public.list_members from anon;
revoke all on public.list_invites from anon;
revoke all on public.shopping_items from anon;

grant select on public.profiles to authenticated;
grant update (display_name, avatar_url) on public.profiles to authenticated;

grant select on public.shopping_lists to authenticated;
grant select on public.list_members to authenticated;
grant select on public.list_invites to authenticated;

grant select, insert on public.shopping_items to authenticated;
grant update (
    name,
    quantity,
    unit,
    note,
    is_completed,
    deleted_at
) on public.shopping_items to authenticated;

commit;
