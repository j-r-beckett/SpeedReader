# Refactor Command

## Description
Conducts systematic, deep refactoring of code to modernize types, patterns, or architecture. Goes beyond surface-level changes to restructure the entire call chain.

## Usage
```
/refactor <description of what needs to be refactored>
```

## Refactoring Principles

### 1. Go Deep, Not Wide
- **Never add overloads** - Change existing methods to use new types
- **Refactor the entire call chain** - From data creation to validation to consumption
- **Eliminate conversions** - Remove all the scattered type conversions and adapters
- **Update test data creation** - Change how expected/mock data is generated

### 2. Systematic Approach
1. **Map the dependency chain** - Understand what calls what before making changes
2. **Start at the core** - Begin with fundamental types and work outward
3. **Update validation/utility methods** - Make them accept the new types directly
4. **Fix test infrastructure** - Update mocks, test data creation, and assertions
5. **Propagate changes upward** - Update all calling code to use new patterns

### 3. What NOT to Do
- ❌ Add overloaded methods for compatibility
- ❌ Leave conversion calls scattered throughout the codebase
- ❌ Make surface-level signature changes without updating implementation
- ❌ Update only some parts of the call chain
- ❌ Shy away from making deep structural changes

### 4. What TO Do
- ✅ Change method signatures to accept new types directly
- ✅ Update the core logic to work with new types natively
- ✅ Refactor utility methods (like validation) to eliminate conversions
- ✅ Update test data creation to use new types from the start
- ✅ Remove old helper methods that are no longer needed
- ✅ Be aggressive about eliminating legacy patterns

### 5. Testing During Refactoring
- Build frequently to catch compilation errors early
- Comment out complex dependencies temporarily if needed
- Focus on getting core functionality working before re-enabling auxiliary features
- Use TodoWrite tool to track progress systematically

## Example Scenarios

### Type System Modernization
When replacing `List<(double X, double Y)>` with `RotatedRectangle`:
1. Update validation methods to accept `RotatedRectangle` directly
2. Change test data creation to return `RotatedRectangle` objects
3. Update all intermediate processing to work with the new type
4. Remove old conversion helper methods

### API Refactoring
When changing method signatures:
1. Update the core method signature and implementation
2. Fix all calling code to pass the new parameter types
3. Update mock objects and test utilities
4. Ensure the entire chain uses the new pattern consistently

## Success Criteria
- ✅ Build succeeds with 0 warnings and 0 errors
- ✅ No conversion calls scattered in the codebase
- ✅ New types are used natively throughout the call chain
- ✅ Test infrastructure works with new types directly
- ✅ Legacy helper methods have been removed