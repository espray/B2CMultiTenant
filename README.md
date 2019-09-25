# B2CMultiTenant
Use IEF policies to support multi-tenancy for apps


## User journeys
1. Sign in/up and create a new tenant
2. Sign (existing user of a tenant) - requires tenant name in signin url
3. Member signup to existing tenant?

## REST functions

### Used by policies only
1. Create new tenant
2. Get user's role in a tenant (using tenant name)

### Available to apps
1. Get user's role in a tenant (using tenant id)
2. Get user's tenants