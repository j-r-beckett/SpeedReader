#!/bin/bash

echo "=== FFmpeg Cross-Platform Docker Tests ==="

# Configuration
TEST_TIMEOUT=300
BUILD_TIMEOUT=600
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../../.." && pwd)"
CORE_TEST_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Test scenarios
LINUX_SCENARIOS=(
    "linux-with-ffmpeg-root"
    "linux-with-ffmpeg-nonroot"
    "linux-no-ffmpeg-root"
    "linux-no-ffmpeg-nonroot"
)

WINDOWS_SCENARIOS=(
    "windows-with-ffmpeg-admin"
    "windows-with-ffmpeg-user"
    "windows-no-ffmpeg-admin"
    "windows-no-ffmpeg-user"
)

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check Docker
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed or not in PATH"
        exit 1
    fi
    
    # Check Docker daemon
    if ! timeout 5s docker info >/dev/null 2>&1; then
        log_error "Docker daemon is not running or not responding"
        exit 1
    fi
    
    # Check .NET
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET is not installed or not in PATH"
        exit 1
    fi
    
    log_success "Prerequisites check passed"
}

# Build wheft executable
build_executable() {
    log_info "Building wheft executable..."
    
    cd "$ROOT_DIR"
    
    # Build in release mode for Docker testing
    if ! timeout $BUILD_TIMEOUT dotnet build Src/Core/Core.csproj -c Release; then
        log_error "Failed to build Core project"
        exit 1
    fi
    
    # Publish for self-contained deployment
    if ! timeout $BUILD_TIMEOUT dotnet publish Src/Core/Core.csproj -c Release; then
        log_error "Failed to publish Core project"
        exit 1
    fi
    
    # Verify executable exists
    local executable_path="Src/Core/bin/Release/net10.0/linux-x64/publish/wheft"
    if [[ ! -f "$executable_path" ]]; then
        log_error "Published executable not found at $executable_path"
        exit 1
    fi
    
    log_success "Executable built successfully"
}

# Build Docker image for scenario
build_docker_image() {
    local scenario="$1"
    local image_name="wheft-test-$scenario"
    
    log_info "Building Docker image for $scenario..."
    
    # Determine Dockerfile path
    local dockerfile_path
    if [[ $scenario == windows-* ]]; then
        local ffmpeg_status=$(echo "$scenario" | cut -d'-' -f2-3)  # with-ffmpeg or no-ffmpeg
        local user_type=$(echo "$scenario" | cut -d'-' -f4)        # admin or user
        dockerfile_path="$CORE_TEST_DIR/docker/windows/$ffmpeg_status-$user_type"
    else
        local ffmpeg_status=$(echo "$scenario" | cut -d'-' -f2-3)  # with-ffmpeg or no-ffmpeg
        local user_type=$(echo "$scenario" | cut -d'-' -f4)        # root or nonroot
        dockerfile_path="$CORE_TEST_DIR/docker/linux/$ffmpeg_status-$user_type"
    fi
    
    if [[ ! -d "$dockerfile_path" ]]; then
        log_error "Dockerfile directory not found: $dockerfile_path"
        return 1
    fi
    
    # Check if image already exists
    if docker images -q "$image_name" | grep -q .; then
        log_info "Image $image_name already exists, skipping build"
        return 0
    fi
    
    # Build the image
    cd "$dockerfile_path"
    if ! timeout $BUILD_TIMEOUT docker build -t "$image_name" .; then
        log_error "Failed to build Docker image for $scenario"
        return 1
    fi
    
    log_success "Built Docker image for $scenario"
    return 0
}

# Run test scenario
run_scenario_test() {
    local scenario="$1"
    local image_name="wheft-test-$scenario"
    
    log_info "Running test for scenario: $scenario"
    
    # Create container
    local container_id
    container_id=$(docker create "$image_name")
    if [[ $? -ne 0 || -z "$container_id" ]]; then
        log_error "Failed to create container for $scenario"
        return 1
    fi
    
    # Cleanup function
    cleanup_container() {
        docker stop "$container_id" >/dev/null 2>&1
        docker rm "$container_id" >/dev/null 2>&1
    }
    
    # Set trap to cleanup on exit
    trap cleanup_container EXIT
    
    # Start container
    if ! docker start "$container_id" >/dev/null; then
        log_error "Failed to start container for $scenario"
        cleanup_container
        return 1
    fi
    
    # Wait a moment for container to fully start
    sleep 2
    
    # Copy executable to container
    local executable_path="$ROOT_DIR/Src/Core/bin/Release/net10.0/linux-x64/publish/wheft"
    local target_path
    if [[ $scenario == windows-* ]]; then
        target_path="C:\\app\\wheft.exe"
    else
        target_path="/app/wheft"
    fi
    
    if ! docker cp "$executable_path" "$container_id:$target_path"; then
        log_error "Failed to copy executable to container for $scenario"
        cleanup_container
        return 1
    fi
    
    # Make executable on Linux
    if [[ $scenario == linux-* ]]; then
        docker exec "$container_id" chmod +x /app/wheft
    fi
    
    # Copy and run verification script
    local script_name="verify-resolution.cs"
    local script_path="$CORE_TEST_DIR/scripts/$script_name"
    local target_script_path
    if [[ $scenario == windows-* ]]; then
        target_script_path="C:\\app\\$script_name"
    else
        target_script_path="/app/$script_name"
    fi
    
    if ! docker cp "$script_path" "$container_id:$target_script_path"; then
        log_error "Failed to copy test script to container for $scenario"
        cleanup_container
        return 1
    fi
    
    # Run the test
    local output
    if [[ $scenario == windows-* ]]; then
        output=$(timeout $TEST_TIMEOUT docker exec "$container_id" powershell -Command "dotnet run C:\\app\\$script_name" 2>&1)
    else
        output=$(timeout $TEST_TIMEOUT docker exec "$container_id" dotnet run "/app/$script_name" 2>&1)
    fi
    
    local exit_code=$?
    
    echo "--- Output for $scenario ---"
    echo "$output"
    echo "--- End Output ---"
    
    cleanup_container
    trap - EXIT
    
    if [[ $exit_code -eq 0 ]]; then
        log_success "Test passed for $scenario"
        return 0
    else
        log_error "Test failed for $scenario (exit code: $exit_code)"
        return 1
    fi
}

# Main test execution
run_tests() {
    local platform="$1"
    local scenarios
    
    if [[ "$platform" == "linux" ]]; then
        scenarios=("${LINUX_SCENARIOS[@]}")
    elif [[ "$platform" == "windows" ]]; then
        scenarios=("${WINDOWS_SCENARIOS[@]}")
    else
        scenarios=("${LINUX_SCENARIOS[@]}" "${WINDOWS_SCENARIOS[@]}")
    fi
    
    local total_tests=${#scenarios[@]}
    local passed_tests=0
    local failed_tests=0
    
    log_info "Running $total_tests test scenarios..."
    
    for scenario in "${scenarios[@]}"; do
        echo
        log_info "=== Testing scenario: $scenario ==="
        
        # Build Docker image
        if build_docker_image "$scenario"; then
            # Run test
            if run_scenario_test "$scenario"; then
                ((passed_tests++))
            else
                ((failed_tests++))
            fi
        else
            log_error "Skipping $scenario due to build failure"
            ((failed_tests++))
        fi
    done
    
    echo
    log_info "=== Test Summary ==="
    log_info "Total tests: $total_tests"
    log_success "Passed: $passed_tests"
    if [[ $failed_tests -gt 0 ]]; then
        log_error "Failed: $failed_tests"
        exit 1
    else
        log_success "All tests passed!"
    fi
}

# Parse command line arguments
PLATFORM="all"
while [[ $# -gt 0 ]]; do
    case $1 in
        --platform)
            PLATFORM="$2"
            shift 2
            ;;
        --help|-h)
            echo "Usage: $0 [--platform linux|windows|all]"
            echo "  --platform: Run tests for specific platform (default: all)"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Main execution
main() {
    check_prerequisites
    build_executable
    run_tests "$PLATFORM"
}

# Run main function
main "$@"