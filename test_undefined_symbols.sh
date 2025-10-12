#!/bin/bash

# Test compilation to identify undefined symbols
echo "Testing compilation to identify undefined symbols..."

# Try to compile the test file
cd /workspace

# Check if we can compile the test file
echo "Attempting to compile test_compile.cpp..."
g++ -I. -c test_compile.cpp -o test_compile.o 2>&1 | tee compile_errors.log

# Check the results
if [ -f test_compile.o ]; then
    echo "✅ Basic compilation successful"
    rm test_compile.o
else
    echo "❌ Compilation failed - checking errors..."
    cat compile_errors.log
fi

# Try to compile with different flags to catch more issues
echo ""
echo "Testing with stricter compilation flags..."
g++ -I. -Wall -Wextra -c test_compile.cpp -o test_compile.o 2>&1 | tee compile_errors_strict.log

if [ -f test_compile.o ]; then
    echo "✅ Strict compilation successful"
    rm test_compile.o
else
    echo "❌ Strict compilation failed - checking errors..."
    cat compile_errors_strict.log
fi

echo "Compilation test complete."